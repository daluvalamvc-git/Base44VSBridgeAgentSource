using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace Base44AIPilot.Options
{
    public class Base44OptionsPage : DialogPage
    {
        private string _apiKey = string.Empty;
        private string _baseUrl = "https://app.base44.com/api/apps/6a57fda9caabceffcbd70384";
        private int _tokenBudget = 12000;

        [Category("Authentication")]
        [DisplayName("API Key")]
        [Description("Your Base44 API key. Find it at: Base44 Agent Settings → API Docs")]
        [PasswordPropertyText(true)]
        public string ApiKey
        {
            get => _apiKey;
            set => _apiKey = value;
        }

        [Category("Connection")]
        [DisplayName("Base URL")]
        [Description("Base44 agent API endpoint. Default: https://app.base44.com/api/apps/6a57fda9caabceffcbd70384")]
        public string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = value;
        }

        [Category("Performance")]
        [DisplayName("Token Budget")]
        [Description("Maximum tokens to send per request (higher = more context, more credits). Default: 12000")]
        public int TokenBudget
        {
            get => _tokenBudget;
            set => _tokenBudget = value < 1000 ? 1000 : value > 50000 ? 50000 : value;
        }

        [Category("Performance")]
        [DisplayName("Include View Files")]
        [Description("Include .cshtml View files in solution context (uses more tokens). Default: true")]
        public bool IncludeViews { get; set; } = true;

        [Category("Performance")]
        [DisplayName("Include Config Files")]
        [Description("Include .json/.xml/.config files in solution context. Default: true")]
        public bool IncludeConfigs { get; set; } = true;
    }
}
