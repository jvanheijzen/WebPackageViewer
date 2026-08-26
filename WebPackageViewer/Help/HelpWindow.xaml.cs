using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WebPackageViewer.Help
{
    public partial class HelpWindow : Window
    {
        private readonly string _html;
        private readonly string _anchor;

        public HelpWindow(
            string title,
            string html,
            string anchor = null)
        {
            InitializeComponent();

            Title = title;
            _html = html ?? string.Empty;
            _anchor = anchor;

            Loaded += HelpWindow_Loaded;
        }

        private async void HelpWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                var userDataFolder =
                    Path.Combine(
                        Path.GetTempPath(),
                        "WebPackageViewer_Help_WebView_" +
                        Process.GetCurrentProcess().Id);

                var environment =
                    await CoreWebView2Environment.CreateAsync(
                        userDataFolder: userDataFolder);

                await HelpWebView.EnsureCoreWebView2Async(
                    environment);

                HelpWebView.NavigationCompleted +=
                    async (s, args) =>
                    {
                        if (string.IsNullOrWhiteSpace(_anchor))
                            return;

                        var safeAnchor =
                            _anchor
                                .Replace("\\", "\\\\")
                                .Replace("'", "\\'");

                        await HelpWebView.CoreWebView2.ExecuteScriptAsync(
                            "location.hash='" +
                            safeAnchor +
                            "';");
                    };

                HelpWebView.NavigateToString(_html);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Help could not be opened.\n\n" +
                    ex.GetBaseException().Message,
                    "Web Package Help",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Close();
            }
        }
    }
}
