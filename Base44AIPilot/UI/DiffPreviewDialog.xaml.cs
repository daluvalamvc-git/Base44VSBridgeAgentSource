using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Base44AIPilot.Models;

namespace Base44AIPilot.UI
{
    public partial class DiffPreviewDialog : Window
    {
        private readonly ParsedResponse _response;

        public DiffPreviewDialog(ParsedResponse response)
        {
            if (response == null) throw new ArgumentNullException("response");
            _response = response;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PopulateChangeList();
        }

        private void PopulateChangeList()
        {
            ChangeListPanel.Children.Clear();

            foreach (var nf in _response.NewFiles)
                ChangeListPanel.Children.Add(CreateChangeItem("NEW", nf.FilePath, nf.Content, Color.FromRgb(144, 238, 144), null));

            foreach (var diff in _response.Diffs)
            {
                var label = "+" + diff.LinesAdded + " / -" + diff.LinesRemoved;
                ChangeListPanel.Children.Add(CreateChangeItem("MOD", diff.FilePath, diff.UnifiedDiff, Color.FromRgb(255, 255, 153), label));
            }
        }

        private UIElement CreateChangeItem(string tag, string filePath, string content, Color bgColor, string extraLabel)
        {
            var border = new Border
            {
                Margin          = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                CornerRadius    = new CornerRadius(4),
            };

            var panel = new StackPanel { Orientation = Orientation.Vertical };

            var header = new Border
            {
                Background = new SolidColorBrush(bgColor) { Opacity = 0.3 },
                Padding    = new Thickness(8, 4, 8, 4),
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tagBlock = new TextBlock
            {
                Text             = "[" + tag + "]",
                FontWeight       = FontWeights.Bold,
                FontSize         = 11,
                Margin           = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(tagBlock, 0);

            var pathBlock = new TextBlock
            {
                Text             = filePath,
                FontFamily       = new FontFamily("Consolas, Courier New"),
                FontSize         = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(pathBlock, 1);

            var applyBtn = new Button
            {
                Content   = "Apply",
                Padding   = new Thickness(10, 3, 10, 3),
                Margin    = new Thickness(6, 0, 0, 0),
                Tag       = filePath + "\x00" + content,
                IsEnabled = File.Exists(filePath) || tag == "NEW",
            };
            applyBtn.Click += ApplyButton_Click;
            Grid.SetColumn(applyBtn, 3);

            headerGrid.Children.Add(tagBlock);
            headerGrid.Children.Add(pathBlock);
            headerGrid.Children.Add(applyBtn);

            if (extraLabel != null)
            {
                var extraBlock = new TextBlock
                {
                    Text             = extraLabel,
                    FontSize         = 10,
                    Opacity          = 0.7,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin           = new Thickness(0, 0, 6, 0),
                };
                Grid.SetColumn(extraBlock, 2);
                headerGrid.Children.Add(extraBlock);
            }

            header.Child = headerGrid;
            panel.Children.Add(header);

            var textBox = new TextBox
            {
                Text                          = content,
                IsReadOnly                    = true,
                FontFamily                    = new FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize                      = 11,
                Background                    = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground                    = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                MaxHeight                     = 250,
                Padding                       = new Thickness(8, 6, 8, 6),
                BorderThickness               = new Thickness(0),
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping                  = TextWrapping.NoWrap,
                AcceptsReturn                 = true,
            };
            panel.Children.Add(textBox);
            border.Child = panel;
            return border;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var raw = btn.Tag as string;
            if (raw == null) return;

            var sep   = raw.IndexOf('\x00');
            var fPath = sep >= 0 ? raw.Substring(0, sep) : raw;
            var fContent = sep >= 0 ? raw.Substring(sep + 1) : string.Empty;

            try
            {
                var dir = Path.GetDirectoryName(fPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(fPath, fContent);
                btn.Content   = "✓ Applied";
                btn.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to write " + fPath + ":\n" + ex.Message,
                    "Apply Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ApplyAllButton_Click(object sender, RoutedEventArgs e)
        {
            int applied = 0, failed = 0;
            foreach (var nf in _response.NewFiles)
            {
                try
                {
                    var dir = Path.GetDirectoryName(nf.FilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(nf.FilePath, nf.Content);
                    applied++;
                }
                catch { failed++; }
            }

            var msg = "Applied " + applied + " new file(s). " + (failed > 0 ? failed + " failed." : string.Empty);
            if (_response.Diffs.Count > 0)
                msg += "\n\n" + _response.Diffs.Count + " diff(s) require manual review — copy from the preview above.";

            MessageBox.Show(msg, "Apply Complete", MessageBoxButton.OK,
                failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            Close();
        }
    }
}
