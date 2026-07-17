using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Base44AIPilot.Models;
using Base44AIPilot.Options;
using Base44AIPilot.Services;
using Base44AIPilot.UI;
using Microsoft.VisualStudio.Shell;

namespace Base44AIPilot.ToolWindows
{
    public partial class Base44ChatWindowControl : UserControl
    {
        private SolutionReader    _reader;
        private ApiClient         _apiClient;
        private Base44OptionsPage _options;
        private IServiceProvider  _serviceProvider;

        private string            _currentIntent      = "answer_code_qa";
        private string            _conversationId;
        private ParsedResponse    _lastParsedResponse;
        private List<ChunkedFile> _cachedChunks       = new List<ChunkedFile>();

        public Base44ChatWindowControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // ------------------------------------------------------------------ //

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Base44AIPilotPackage.Instance == null) return;

            _serviceProvider = Base44AIPilotPackage.Instance;
            _options         = Base44AIPilotPackage.Instance.GetDialogPage(typeof(Base44OptionsPage)) as Base44OptionsPage;
            _reader          = new SolutionReader(_serviceProvider);
            _apiClient       = new ApiClient(_options);

            RefreshSolutionContext();
        }

        public void SetIntent(string intent, string autoPrompt, string promptHint)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _currentIntent = intent;

            foreach (ComboBoxItem item in IntentCombo.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == intent)
                {
                    IntentCombo.SelectedItem = item;
                    break;
                }
            }

            if (autoPrompt != null)
            {
                PromptBox.Text = autoPrompt;
                var unused = SendAsync(autoPrompt, intent);
            }
            else if (promptHint != null)
            {
                PromptBox.Text = string.Empty;
                AppendSystemMessage(promptHint);
                PromptBox.Focus();
            }
        }

        // ------------------------------------------------------------------ //

        private void RefreshContextButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            RefreshSolutionContext();
        }

        private void PromptBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                var unused = OnSendClickedAsync();
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var unused = OnSendClickedAsync();
        }

        private void ReviewChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastParsedResponse == null) return;
            var dialog = new DiffPreviewDialog(_lastParsedResponse);
            dialog.ShowDialog();
            ApplyChangesBar.Visibility = Visibility.Collapsed;
        }

        private void DismissChangesButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyChangesBar.Visibility = Visibility.Collapsed;
        }

        // ------------------------------------------------------------------ //

        private async Task OnSendClickedAsync()
        {
            var prompt = PromptBox.Text.Trim();
            if (string.IsNullOrEmpty(prompt)) return;
            PromptBox.Text = string.Empty;

            var selectedItem = IntentCombo.SelectedItem as ComboBoxItem;
            var intent = selectedItem != null && selectedItem.Tag != null
                ? selectedItem.Tag.ToString()
                : _currentIntent;

            await SendAsync(prompt, intent);
        }

        // UI-thread switch helper — uses WPF Dispatcher, no Threading assembly needed.
        private Task SwitchToUIThreadAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                (Action)(() => tcs.SetResult(true)));
            return tcs.Task;
        }

        private async Task SendAsync(string prompt, string intent)
        {
            if (intent == null) intent = _currentIntent;

            SetInputEnabled(false);
            AppendUserMessage(prompt);
            var thinkingEl = AppendSystemMessage("⏳ Thinking…");

            try
            {
                // Capture VS-COM data on the UI thread first (we are already on it here).
                await SwitchToUIThreadAsync();
                RefreshSolutionContext();
                var solutionName = SolutionNameText.Text;
                string activeFile = null;
                try
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    activeFile = _reader != null ? _reader.GetActiveFilePath() : null;
                }
                catch { /* ignore if not on UI thread */ }
                var chunks = _cachedChunks;

                var request = new ChatRequest
                {
                    ConversationId = _conversationId,
                    Intent         = intent,
                    Prompt         = prompt,
                    SolutionName   = solutionName,
                    ActiveFilePath = activeFile,
                    Files          = chunks
                };

                // HTTP call on a background thread.
                var response = await Task.Run(() => _apiClient.ChatAsync(request)).ConfigureAwait(false);
                _conversationId = response.ConversationId;

                // Back to UI thread to update controls.
                await SwitchToUIThreadAsync();
                RemoveMessage(thinkingEl);

                var parsed = DiffParser.Parse(response.Response);
                _lastParsedResponse = parsed;
                AppendAgentMessage(parsed.PlainText);

                if (parsed.HasChanges)
                {
                    ChangeSummaryText.Text     = "AI suggested " + (parsed.NewFiles.Count + parsed.Diffs.Count) + " file change(s)";
                    ChangeDetailsText.Text     = parsed.NewFiles.Count + " new  ·  " + parsed.Diffs.Count + " modified";
                    ApplyChangesBar.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                await SwitchToUIThreadAsync();
                RemoveMessage(thinkingEl);
                AppendSystemMessage("❌ " + ex.Message);
            }
            finally
            {
                await SwitchToUIThreadAsync();
                SetInputEnabled(true);
            }
        }

        // ------------------------------------------------------------------ //

        private void RefreshSolutionContext()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_reader == null || _apiClient == null) return;

            try
            {
                var info  = _reader.GetSolutionInfo();
                var files = _reader.GetSolutionFiles();

                SolutionNameText.Text = info.SolutionName;
                FileCountText.Text    = files.Count + " files";

                var chunkReq = new ChunkRequest
                {
                    Files          = files,
                    ActiveFilePath = _reader.GetActiveFilePath(),
                    TokenBudget    = _options != null ? _options.TokenBudget : 12000
                };

                var chunks = _apiClient.ChunkContext(chunkReq);
                _cachedChunks = chunks.ChunkedFiles;

                FileCountText.Text = files.Count + " files · " +
                    chunks.IncludedFull + " full · " +
                    chunks.IncludedSummary + " summarised · ~" +
                    chunks.TokensUsed + " tokens";
            }
            catch (Exception ex)
            {
                FileCountText.Text = "(context error: " + ex.Message + ")";
            }
        }

        // ------------------------------------------------------------------ //

        private void AppendUserMessage(string text)
        {
            var border = new Border { Style = FindResource("UserBubble") as Style };
            border.Child = new TextBlock
            {
                Text         = text,
                Foreground   = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                FontSize     = 12,
            };
            ChatPanel.Children.Add(border);
            ChatScrollViewer.ScrollToBottom();
        }

        private void AppendAgentMessage(string text)
        {
            var border = new Border { Style = FindResource("AgentBubble") as Style };
            border.Child = new TextBlock
            {
                Text         = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize     = 12,
            };
            ChatPanel.Children.Add(border);
            ChatScrollViewer.ScrollToBottom();
        }

        private UIElement AppendSystemMessage(string text)
        {
            var tb = new TextBlock
            {
                Text         = text,
                Foreground   = Brushes.Gray,
                FontSize     = 11,
                FontStyle    = FontStyles.Italic,
                Margin       = new Thickness(4, 2, 4, 2),
                TextWrapping = TextWrapping.Wrap,
            };
            ChatPanel.Children.Add(tb);
            ChatScrollViewer.ScrollToBottom();
            return tb;
        }

        private void RemoveMessage(UIElement el)
        {
            if (el != null && ChatPanel.Children.Contains(el))
                ChatPanel.Children.Remove(el);
        }

        private void SetInputEnabled(bool enabled)
        {
            PromptBox.IsEnabled   = enabled;
            SendButton.IsEnabled  = enabled;
            IntentCombo.IsEnabled = enabled;
        }
    }
}
