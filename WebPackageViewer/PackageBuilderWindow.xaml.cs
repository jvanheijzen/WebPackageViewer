using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using WebPackageViewer.CourseCatalog;
using WebPackageViewer.Licensing;

namespace WebPackageViewer
{
    public partial class PackageBuilderWindow : Window
    {
        private string _lastSuggestedOutput;
        private string _lastSuggestedModuleName;
        private readonly CourseCatalogService _courseCatalog =
            new CourseCatalogService();

        public PackageBuilderWindow()
        {
            InitializeComponent();
            ReloadCourses();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RequireOfflineLicenseCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            var enabled = RequireOfflineLicenseCheckBox.IsChecked == true;

            CourseComboBox.IsEnabled = enabled;
            AddCourseButton.IsEnabled = enabled;
            ProductCodeTextBox.IsEnabled = enabled;
            CourseNameTextBox.IsEnabled = enabled;
            CourseVersionTextBox.IsEnabled = enabled;
            ModuleIdTextBox.IsEnabled = enabled;
            ModuleNameTextBox.IsEnabled = enabled;
        }

        private void ReloadCourses(string selectProductCode = null)
        {
            var courses = _courseCatalog.Load();

            CourseComboBox.ItemsSource = courses;

            if (!string.IsNullOrWhiteSpace(selectProductCode))
            {
                CourseComboBox.SelectedItem = courses.FirstOrDefault(c =>
                    string.Equals(
                        c.ProductCode,
                        selectProductCode,
                        StringComparison.OrdinalIgnoreCase));
            }
            else if (courses.Count > 0 && CourseComboBox.SelectedIndex < 0)
            {
                CourseComboBox.SelectedIndex = 0;
            }
        }

        private void AddCourseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddCourseWindow
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _courseCatalog.Add(dialog.Course);
                ReloadCourses(dialog.Course.ProductCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Course Catalog",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void CourseComboBox_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var course = CourseComboBox.SelectedItem as CourseDefinition;

            ProductCodeTextBox.Text = course?.ProductCode ?? string.Empty;
            CourseNameTextBox.Text = course?.CourseName ?? string.Empty;
            CourseVersionTextBox.Text = course?.CourseVersion ?? string.Empty;
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
                }
            }

            if (dialog.ShowDialog(this) == true)
            {
                OutputFileTextBox.Text = dialog.FileName;
                _lastSuggestedOutput = null;
            }
        }

        private void SourceFolderTextBox_TextChanged(
            object sender,
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
                {
                    WindowTitleTextBox.Text = title;

                    // Default the module name to the HTML/window title, but preserve
                    // a module name that the user has manually edited.
                    if (string.IsNullOrWhiteSpace(ModuleNameTextBox.Text) ||
                        string.Equals(
                            ModuleNameTextBox.Text,
                            _lastSuggestedModuleName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        ModuleNameTextBox.Text = title;
                        _lastSuggestedModuleName = title;
                    }
                }

                var sizeMatch = Regex.Match(
                    html,
                    @"<!--\s*(\d{3,5})\s+(\d{3,5})\s*-->");

                if (sizeMatch.Success)
                {
                    WindowSizeTextBox.Text =
                        sizeMatch.Groups[1].Value + "x" +
                        sizeMatch.Groups[2].Value;
                }

                // Convenience only: detect "Module 0" / "Module 12" in the
                // HTML title and populate a normalized module identifier.
                var moduleMatch = Regex.Match(
                    title ?? string.Empty,
                    @"\bModule\s+(\d+)\b",
                    RegexOptions.IgnoreCase);

                if (moduleMatch.Success &&
                    string.IsNullOrWhiteSpace(ModuleIdTextBox.Text))
                {
                    int moduleNumber;

                    if (int.TryParse(moduleMatch.Groups[1].Value, out moduleNumber))
                        ModuleIdTextBox.Text = "M" + moduleNumber.ToString("00");
                }

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

            var requireOfflineLicense =
                RequireOfflineLicenseCheckBox.IsChecked == true;

            var course = CourseComboBox.SelectedItem as CourseDefinition;
            var moduleId = ModuleIdTextBox.Text?.Trim();
            var moduleName = ModuleNameTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(sourceFolder) ||
                !Directory.Exists(sourceFolder))
            {
                MessageBox.Show(this, "Select a valid Web site folder.",
                    "Web Package Builder", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputFile))
            {
                MessageBox.Show(this, "Select an output EXE file.",
                    "Web Package Builder", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (requireOfflineLicense && course == null)
            {
                MessageBox.Show(this,
                    "Select a course from the course catalog.",
                    "Web Package Builder", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (requireOfflineLicense &&
                string.IsNullOrWhiteSpace(moduleId))
            {
                MessageBox.Show(this,
                    "Enter a Module ID, such as M00.",
                    "Web Package Builder", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ModuleIdTextBox.Focus();
                return;
            }

            if (requireOfflineLicense &&
                string.IsNullOrWhiteSpace(moduleName))
            {
                MessageBox.Show(this,
                    "Enter the Module Name.",
                    "Web Package Builder", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ModuleNameTextBox.Focus();
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
                        windowSize,
                        requireOfflineLicense,
                        course,
                        moduleId,
                        moduleName));

                if (!result.Success)
                {
                    StatusTextBlock.Text = "Build failed.";
                    MessageBox.Show(this, result.ErrorMessage,
                        "Web Package Builder", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                StatusTextBlock.Text = requireOfflineLicense
                    ? "Licensed package created successfully."
                    : "Package created successfully.";

                MessageBox.Show(
                    this,
                    (requireOfflineLicense
                        ? "Licensed package created successfully:\n\n"
                        : "Package created successfully:\n\n") +
                    outputFile,
                    "Web Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Build failed.";
                MessageBox.Show(this, ex.Message,
                    "Web Package Builder", MessageBoxButton.OK,
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
            string windowSize,
            bool requireOfflineLicense,
            CourseDefinition course,
            string moduleId,
            string moduleName)
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

                var config =
                    "{\r\n" +
                    "  \"VirtualPath\": \"" + EscapeJsonString(virtualPath) + "\",\r\n" +
                    "  \"InitialUrl\": \"" + EscapeJsonString(initialUrl) + "\",\r\n" +
                    "  \"WindowTitle\": \"" + EscapeJsonString(windowTitle) + "\",\r\n" +
                    "  \"WindowSize\": \"" + EscapeJsonString(windowSize) + "\"\r\n" +
                    "}\r\n";

                File.WriteAllText(
                    Path.Combine(stageFolder, "WebPackageViewer.config.json"),
                    config);

                if (requireOfflineLicense)
                {
                    var requirement = new OfflineLicenseRequirement
                    {
                        Version = 1,
                        CourseId = course.ProductCode,
                        CourseName = course.CourseName,
                        CourseVersion = course.CourseVersion,
                        ModuleId = moduleId,
                        ModuleName = moduleName
                    };

                    OfflineLicenseSerializer.WriteRequirement(
                        Path.Combine(
                            stageFolder,
                            OfflineLicenseManager.RequirementFileName),
                        requirement);
                }

                var packager = new FilePackager();
                var generatedZip = packager.ZipFolder(stageFolder, zipFile);

                if (string.IsNullOrWhiteSpace(generatedZip))
                    return BuildResult.Fail(
                        "Failed to create the Web site ZIP file.\n\n" +
                        packager.ErrorMessage);

                var packageExe = Assembly.GetExecutingAssembly().Location;

                if (!packager.PackageFile(outputFile, packageExe, generatedZip))
                    return BuildResult.Fail(
                        "Failed to create the packaged executable.\n\n" +
                        packager.ErrorMessage);

                if (!File.Exists(outputFile))
                    return BuildResult.Fail(
                        "Packaging completed but the output EXE was not created.");

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
                    Path.Combine(destinationFolder, Path.GetFileName(directory));

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
                return new BuildResult { Success = true };
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
