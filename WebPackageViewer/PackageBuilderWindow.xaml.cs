using Microsoft.Win32;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace WebPackageViewer
{
    public partial class PackageBuilderWindow : Window
    {
        private string _lastSuggestedOutput;

        public PackageBuilderWindow()
        {
            InitializeComponent();
        }


        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        private void BrowseSourceButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select the Web site's index.html file",
                Filter = "HTML entry point (index.html)|index.html|HTML files (*.html)|*.html|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                FileName = "index.html"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            SourceFolderTextBox.Text = Path.GetDirectoryName(dialog.FileName);

            // Use the selected HTML file as the initial page.
            InitialUrlTextBox.Text = "/" + Path.GetFileName(dialog.FileName);
        }


        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var suggestedName = GetSafeFileName(WindowTitleTextBox.Text);

            if (string.IsNullOrWhiteSpace(suggestedName))
                suggestedName = "Packaged";

            var dialog = new SaveFileDialog
            {
                Title = "Create Web Package executable",
                Filter = "Executable files (*.exe)|*.exe",
                AddExtension = true,
                DefaultExt = ".exe",
                FileName = suggestedName + ".exe"
            };

            if (!string.IsNullOrWhiteSpace(OutputFileTextBox.Text))
            {
                try
                {
                    var existingFolder = Path.GetDirectoryName(OutputFileTextBox.Text);

                    if (Directory.Exists(existingFolder))
                        dialog.InitialDirectory = existingFolder;

                    var existingName = Path.GetFileName(OutputFileTextBox.Text);

                    if (!string.IsNullOrWhiteSpace(existingName))
                        dialog.FileName = existingName;
                }
                catch
                {
                    // Ignore malformed manually-entered paths.
                }
            }

            if (dialog.ShowDialog(this) == true)
            {
                OutputFileTextBox.Text = dialog.FileName;
                _lastSuggestedOutput = null;
            }
        }


        private void SourceFolderTextBox_TextChanged(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            PopulateSiteInformation();
        }


        private void PopulateSiteInformation()
        {
            var sourceFolder = SourceFolderTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(sourceFolder) ||
                !Directory.Exists(sourceFolder))
                return;

            var indexPath = Path.Combine(sourceFolder, "index.html");

            if (!File.Exists(indexPath))
                return;

            try
            {
                var html = File.ReadAllText(indexPath);

                // Read the normal HTML <title>.
                var titleMatch = Regex.Match(
                    html,
                    @"<title\b[^>]*>(.*?)</title>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                string title;

                if (titleMatch.Success)
                    title = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                else
                    title = new DirectoryInfo(sourceFolder).Name;

                if (!string.IsNullOrWhiteSpace(title))
                    WindowTitleTextBox.Text = title;


                // iSpring and some other generators include dimensions
                // in a comment such as:
                //
                //     <!-- 1296 744 -->
                //
                // If present, use them as the initial window dimensions.
                var sizeMatch = Regex.Match(
                    html,
                    @"<!--\s*(\d{3,5})\s+(\d{3,5})\s*-->");

                if (sizeMatch.Success)
                {
                    WindowSizeTextBox.Text =
                        sizeMatch.Groups[1].Value + "x" +
                        sizeMatch.Groups[2].Value;
                }


                // Suggest an output filename, but don't overwrite a path
                // the user has manually selected.
                var safeName = GetSafeFileName(title);

                var parentFolder = Directory.GetParent(sourceFolder)?.FullName;

                if (string.IsNullOrWhiteSpace(parentFolder))
                    parentFolder = sourceFolder;

                var suggestedOutput =
                    Path.Combine(parentFolder, safeName + ".exe");

                if (string.IsNullOrWhiteSpace(OutputFileTextBox.Text) ||
                    string.Equals(
                        OutputFileTextBox.Text,
                        _lastSuggestedOutput,
                        StringComparison.OrdinalIgnoreCase))
                {
                    OutputFileTextBox.Text = suggestedOutput;
                    _lastSuggestedOutput = suggestedOutput;
                }
            }
            catch
            {
                // A malformed or unusual HTML file should not make
                // the builder unusable. The user can enter values manually.
            }
        }


        private async void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            var sourceFolder = SourceFolderTextBox.Text?.Trim();
            var outputFile = OutputFileTextBox.Text?.Trim();
            var windowTitle = WindowTitleTextBox.Text?.Trim();
            var initialUrl = InitialUrlTextBox.Text?.Trim();
            var virtualPath = VirtualPathTextBox.Text?.Trim();
            var windowSize = WindowSizeTextBox.Text?.Trim();


            if (string.IsNullOrWhiteSpace(sourceFolder) ||
                !Directory.Exists(sourceFolder))
            {
                MessageBox.Show(
                    this,
                    "Select a valid Web site folder.",
                    "Web Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            if (string.IsNullOrWhiteSpace(outputFile))
            {
                MessageBox.Show(
                    this,
                    "Select an output EXE file.",
                    "Web Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }


            if (string.IsNullOrWhiteSpace(initialUrl))
                initialUrl = "/index.html";

            if (string.IsNullOrWhiteSpace(virtualPath))
                virtualPath = "/";

            if (string.IsNullOrWhiteSpace(windowTitle))
                windowTitle = "West Wind Web Package Viewer";

            if (string.IsNullOrWhiteSpace(windowSize))
                windowSize = "1280x800";


            if (File.Exists(outputFile))
            {
                var answer = MessageBox.Show(
                    this,
                    "The output file already exists:\n\n" +
                    outputFile +
                    "\n\nReplace it?",
                    "Web Package Builder",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                    return;
            }


            BuildButton.IsEnabled = false;
            StatusTextBlock.Text = "Building package...";


            try
            {
                var result = await Task.Run(() =>
                    BuildPackage(
                        sourceFolder,
                        outputFile,
                        windowTitle,
                        initialUrl,
                        virtualPath,
                        windowSize));


                if (!result.Success)
                {
                    StatusTextBlock.Text = "Build failed.";

                    MessageBox.Show(
                        this,
                        result.ErrorMessage,
                        "Web Package Builder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }


                StatusTextBlock.Text = "Package created successfully.";

                MessageBox.Show(
                    this,
                    "Package created successfully:\n\n" +
                    outputFile,
                    "Web Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Build failed.";

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Web Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                BuildButton.IsEnabled = true;
            }
        }


        private BuildResult BuildPackage(
            string sourceFolder,
            string outputFile,
            string windowTitle,
            string initialUrl,
            string virtualPath,
            string windowSize)
        {
            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "WebPackageBuilder_" + Guid.NewGuid().ToString("N"));

            var stageFolder = Path.Combine(tempRoot, "WebRoot");
            var zipFile = Path.Combine(tempRoot, "WebPackage.zip");

            try
            {
                Directory.CreateDirectory(stageFolder);

                CopyDirectory(sourceFolder, stageFolder);


                // WebViewerConfiguration currently expects a very specific
                // JSON layout, including one space following each colon.
                //
                // Write that exact format instead of relying on a JSON
                // serializer that might format whitespace differently.
                var config =
                    "{\r\n" +
                    "  \"VirtualPath\": \"" + EscapeJsonString(virtualPath) + "\",\r\n" +
                    "  \"InitialUrl\": \"" + EscapeJsonString(initialUrl) + "\",\r\n" +
                    "  \"WindowTitle\": \"" + EscapeJsonString(windowTitle) + "\",\r\n" +
                    "  \"WindowSize\": \"" + EscapeJsonString(windowSize) + "\"\r\n" +
                    "}\r\n";

                File.WriteAllText(
                    Path.Combine(
                        stageFolder,
                        "WebPackageViewer.config.json"),
                    config);


                var packager = new FilePackager();

                var generatedZip =
                    packager.ZipFolder(stageFolder, zipFile);

                if (string.IsNullOrWhiteSpace(generatedZip))
                {
                    return BuildResult.Fail(
                        "Failed to create the Web site ZIP file.\n\n" +
                        packager.ErrorMessage);
                }


                var packageExe =
                    Assembly.GetExecutingAssembly().Location;


                if (!packager.PackageFile(
                    outputFile,
                    packageExe,
                    generatedZip))
                {
                    return BuildResult.Fail(
                        "Failed to create the packaged executable.\n\n" +
                        packager.ErrorMessage);
                }


                if (!File.Exists(outputFile))
                {
                    return BuildResult.Fail(
                        "Packaging completed but the output EXE was not created.");
                }


                return BuildResult.Ok();
            }
            catch (Exception ex)
            {
                return BuildResult.Fail(ex.Message);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // Temporary cleanup failure should not invalidate
                    // a successfully-created package.
                }
            }
        }


        private static void CopyDirectory(
            string sourceFolder,
            string destinationFolder)
        {
            Directory.CreateDirectory(destinationFolder);

            foreach (var file in Directory.GetFiles(sourceFolder))
            {
                var destinationFile =
                    Path.Combine(destinationFolder, Path.GetFileName(file));

                File.Copy(file, destinationFile, true);
            }

            foreach (var directory in Directory.GetDirectories(sourceFolder))
            {
                var destinationDirectory =
                    Path.Combine(
                        destinationFolder,
                        Path.GetFileName(directory));

                CopyDirectory(directory, destinationDirectory);
            }
        }


        private static string GetSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Packaged";

            var safeName = value.Trim();

            foreach (var character in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(character, '_');

            safeName = safeName.TrimEnd('.', ' ');

            return string.IsNullOrWhiteSpace(safeName)
                ? "Packaged"
                : safeName;
        }


        private static string EscapeJsonString(string value)
        {
            if (value == null)
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }


        private class BuildResult
        {
            public bool Success { get; private set; }

            public string ErrorMessage { get; private set; }


            public static BuildResult Ok()
            {
                return new BuildResult
                {
                    Success = true
                };
            }


            public static BuildResult Fail(string message)
            {
                return new BuildResult
                {
                    Success = false,
                    ErrorMessage = message
                };
            }
        }
    }
}