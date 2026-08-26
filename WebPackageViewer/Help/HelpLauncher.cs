using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace WebPackageViewer.Help
{
    public static class HelpLauncher
    {
        public const string AdministratorHelpFileName =
            "WebPackageTools-Administrator.html";

        private const string DistributorResourceName =
            "WebPackageViewer.DistributorHelp.html";

        public static void AttachAdministratorHelp(
            Window window,
            string topic)
        {
            if (window == null)
                return;

            window.PreviewKeyDown +=
                (sender, args) =>
                {
                    if (args.Key != Key.F1)
                        return;

                    args.Handled = true;
                    ShowAdministratorHelp(window, topic);
                };
        }

        public static void AttachDistributorHelp(
            Window window,
            string topic)
        {
            if (window == null)
                return;

            window.PreviewKeyDown +=
                (sender, args) =>
                {
                    if (args.Key != Key.F1)
                        return;

                    args.Handled = true;
                    ShowDistributorHelp(window, topic);
                };
        }

        public static void ShowAdministratorHelp(
            Window owner,
            string topic = null)
        {
            var filename =
                FindAdministratorHelpFile();

            if (string.IsNullOrWhiteSpace(filename))
            {
                MessageBox.Show(
                    owner,
                    "The administrator manual is not installed.\n\n" +
                    AdministratorHelpFileName,
                    "Web Package Help",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var html = File.ReadAllText(filename);

            Show(
                owner,
                "Web Package Tools Administrator Manual",
                html,
                topic);
        }

        public static void ShowDistributorHelp(
            Window owner,
            string topic = null)
        {
            string html;

            using (var stream =
                typeof(HelpLauncher)
                    .Assembly
                    .GetManifestResourceStream(
                        DistributorResourceName))
            {
                if (stream == null)
                {
                    MessageBox.Show(
                        owner,
                        "The distributor help resource is not available.",
                        "Web Package Help",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                using (var reader =
                    new StreamReader(
                        stream,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: true))
                {
                    html = reader.ReadToEnd();
                }
            }

            Show(
                owner,
                "Offline Course Package Distributor Guide",
                html,
                topic);
        }

        private static void Show(
            Window owner,
            string title,
            string html,
            string topic)
        {
            var window =
                new HelpWindow(
                    title,
                    html,
                    topic);

            if (owner != null && owner.IsVisible)
                window.Owner = owner;

            window.Show();
        }

        private static string FindAdministratorHelpFile()
        {
            var candidates =
                new List<string>();

            AddCandidate(
                candidates,
                Path.Combine(
                    AppContext.BaseDirectory,
                    AdministratorHelpFileName));

            AddCandidate(
                candidates,
                Path.Combine(
                    Environment.CurrentDirectory,
                    AdministratorHelpFileName));

            AddCandidate(
                candidates,
                Path.Combine(
                    Environment.CurrentDirectory,
                    "Documentation",
                    AdministratorHelpFileName));

            var directory =
                new DirectoryInfo(
                    AppContext.BaseDirectory);

            for (var i = 0;
                directory != null && i < 7;
                i++)
            {
                AddCandidate(
                    candidates,
                    Path.Combine(
                        directory.FullName,
                        "Documentation",
                        AdministratorHelpFileName));

                directory = directory.Parent;
            }

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static void AddCandidate(
            IList<string> candidates,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!candidates.Contains(value))
                candidates.Add(value);
        }
    }
}
