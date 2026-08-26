using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using WebPackageViewer.CourseCatalog;
using WebPackageViewer.Licensing;
using WebPackageViewer.Help;
using Forms = System.Windows.Forms;

namespace WebPackageViewer
{
    public partial class BatchPackageBuilderWindow : Window
    {
        private readonly CourseCatalogService _catalog =
            new CourseCatalogService();

        public ObservableCollection<BatchPackageItem> Items
        {
            get;
        } = new ObservableCollection<BatchPackageItem>();

        public BatchPackageBuilderWindow()
        {
            InitializeComponent();
            DataContext = this;
            HelpLauncher.AttachAdministratorHelp(this, "builder-batch");

            var courses = _catalog.Load();
            CourseComboBox.ItemsSource = courses;

            if (courses.Count > 0)
                CourseComboBox.SelectedIndex = 0;
        }

        private void AddCourseFolderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            using (var dialog =
                new Forms.FolderBrowserDialog())
            {
                dialog.Description =
                    "Select the parent folder containing the module Web site folders.";

                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() !=
                    Forms.DialogResult.OK)
                    return;

                try
                {
                    AddSitesFromRoot(dialog.SelectedPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Batch Package Builder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void BrowseOutputButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            using (var dialog =
                new Forms.FolderBrowserDialog())
            {
                dialog.Description =
                    "Select the folder where the module EXE files will be created.";

                dialog.ShowNewFolderButton = true;

                if (Directory.Exists(
                    OutputFolderTextBox.Text))
                {
                    dialog.SelectedPath =
                        OutputFolderTextBox.Text;
                }

                if (dialog.ShowDialog() ==
                    Forms.DialogResult.OK)
                {
                    OutputFolderTextBox.Text =
                        dialog.SelectedPath;
                }
            }
        }

        private void AddSitesFromRoot(string rootFolder)
        {
            var indexFiles =
                Directory.GetFiles(
                    rootFolder,
                    "index.html",
                    SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();

            if (indexFiles.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "No index.html files were found beneath the selected folder.",
                    "Batch Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var existing =
                new HashSet<string>(
                    Items.Select(i => i.SourceFolder),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var indexFile in indexFiles)
            {
                var sourceFolder =
                    Path.GetDirectoryName(indexFile);

                if (existing.Contains(sourceFolder))
                    continue;

                Items.Add(CreateItem(indexFile));
                existing.Add(sourceFolder);
            }

            if (string.IsNullOrWhiteSpace(
                OutputFolderTextBox.Text))
            {
                OutputFolderTextBox.Text =
                    Path.Combine(
                        rootFolder,
                        "Packaged");
            }
        }

        private static BatchPackageItem CreateItem(
            string indexFile)
        {
            var sourceFolder =
                Path.GetDirectoryName(indexFile);

            var html =
                File.ReadAllText(indexFile);

            var titleMatch =
                Regex.Match(
                    html,
                    @"<title\b[^>]*>(.*?)</title>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            var title =
                titleMatch.Success
                    ? WebUtility.HtmlDecode(
                        titleMatch.Groups[1].Value)
                        .Trim()
                    : new DirectoryInfo(
                        sourceFolder).Name;

            var sizeMatch =
                Regex.Match(
                    html,
                    @"<!--\s*(\d{3,5})\s+(\d{3,5})\s*-->");

            var windowSize =
                sizeMatch.Success
                    ? sizeMatch.Groups[1].Value +
                      "x" +
                      sizeMatch.Groups[2].Value
                    : "1280x800";

            var moduleId =
                string.Empty;

            var moduleMatch =
                Regex.Match(
                    title ?? string.Empty,
                    @"\bModule\s+(\d+)\b",
                    RegexOptions.IgnoreCase);

            int moduleNumber;

            if (moduleMatch.Success &&
                int.TryParse(
                    moduleMatch.Groups[1].Value,
                    out moduleNumber))
            {
                moduleId =
                    "M" +
                    moduleNumber.ToString("00");
            }

            return new BatchPackageItem
            {
                SourceFolder = sourceFolder,
                WindowTitle = title,
                WindowSize = windowSize,
                ModuleId = moduleId,
                ModuleName = title,
                OutputFileName =
                    GetSafeFileName(title) +
                    ".exe",
                Status = "Ready"
            };
        }

        private void RemoveSelectedButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var selected =
                ItemsDataGrid.SelectedItems
                    .Cast<BatchPackageItem>()
                    .ToList();

            foreach (var item in selected)
                Items.Remove(item);
        }

        private void ClearButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Items.Clear();
        }

        private void HelpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            HelpLauncher.ShowAdministratorHelp(
                this,
                "builder-batch");
        }
        private async void BuildAllButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Items.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Add at least one module Web site.",
                    "Batch Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var outputFolder =
                OutputFolderTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(
                outputFolder))
            {
                MessageBox.Show(
                    this,
                    "Select an output folder.",
                    "Batch Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var duplicateOutputs =
                Items
                    .Where(i =>
                        !string.IsNullOrWhiteSpace(
                            i.OutputFileName))
                    .GroupBy(
                        i => i.OutputFileName.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

            if (duplicateOutputs.Count > 0)
            {
                MessageBox.Show(
                    this,
                    "Two or more rows have the same output filename:\n\n" +
                    string.Join("\n", duplicateOutputs) +
                    "\n\nEdit the filenames before building.",
                    "Batch Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Directory.CreateDirectory(outputFolder);

            var requireLicense =
                RequireOfflineLicenseCheckBox.IsChecked ==
                true;

            var course =
                CourseComboBox.SelectedItem
                as CourseDefinition;

            if (requireLicense &&
                course == null)
            {
                MessageBox.Show(
                    this,
                    "Select a course.",
                    "Batch Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            foreach (var item in Items)
            {
                if (requireLicense &&
                    string.IsNullOrWhiteSpace(
                        item.ModuleId))
                {
                    MessageBox.Show(
                        this,
                        "Every licensed module needs a Module ID. Check:\n\n" +
                        item.WindowTitle,
                        "Batch Package Builder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(
                    item.ModuleName))
                {
                    MessageBox.Show(
                        this,
                        "Every module needs a Module Name.",
                        "Batch Package Builder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(
                    item.OutputFileName))
                {
                    MessageBox.Show(
                        this,
                        "Every module needs an output filename.",
                        "Batch Package Builder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            BuildAllButton.IsEnabled = false;

            try
            {
                foreach (var item in Items)
                {
                    item.Status = "Building...";

                    var result =
                        await Task.Run(() =>
                            BuildOne(
                                item,
                                outputFolder,
                                requireLicense,
                                course));

                    item.Status =
                        result.Success
                            ? "Built"
                            : "Failed";

                    if (!result.Success)
                    {
                        MessageBox.Show(
                            this,
                            item.WindowTitle +
                            "\n\n" +
                            result.ErrorMessage,
                            "Batch Build Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        return;
                    }
                }

                MessageBox.Show(
                    this,
                    Items.Count +
                    " package(s) built successfully.",
                    "Batch Package Builder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                BuildAllButton.IsEnabled = true;
            }
        }

        private BuildResult BuildOne(
            BatchPackageItem item,
            string outputFolder,
            bool requireLicense,
            CourseDefinition course)
        {
            var outputFile =
                Path.Combine(
                    outputFolder,
                    GetSafeFileName(
                        Path.GetFileNameWithoutExtension(
                            item.OutputFileName)) +
                    ".exe");

            var tempRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "WebPackageBatch_" +
                    Guid.NewGuid().ToString("N"));

            var stageFolder =
                Path.Combine(
                    tempRoot,
                    "WebRoot");

            var zipFile =
                Path.Combine(
                    tempRoot,
                    "WebPackage.zip");

            try
            {
                Directory.CreateDirectory(stageFolder);
                CopyDirectory(
                    item.SourceFolder,
                    stageFolder);

                var config =
                    "{\r\n" +
                    "  \"VirtualPath\": \"/\",\r\n" +
                    "  \"InitialUrl\": \"/index.html\",\r\n" +
                    "  \"WindowTitle\": \"" +
                    EscapeJsonString(
                        item.WindowTitle) +
                    "\",\r\n" +
                    "  \"WindowSize\": \"" +
                    EscapeJsonString(
                        item.WindowSize) +
                    "\"\r\n" +
                    "}\r\n";

                File.WriteAllText(
                    Path.Combine(
                        stageFolder,
                        "WebPackageViewer.config.json"),
                    config);

                OfflineLicenseRequirement requirement =
                    null;

                if (requireLicense)
                {
                    requirement =
                        new OfflineLicenseRequirement
                        {
                            Version = 1,
                            CourseId =
                                course.ProductCode,
                            CourseName =
                                course.CourseName,
                            CourseVersion =
                                course.CourseVersion,
                            ModuleId =
                                item.ModuleId.Trim(),
                            ModuleName =
                                item.ModuleName.Trim()
                        };

                    OfflineLicenseSerializer.WriteRequirement(
                        Path.Combine(
                            stageFolder,
                            OfflineLicenseManager.RequirementFileName),
                        requirement);
                }

                var packager =
                    new FilePackager();

                var generatedZip =
                    packager.ZipFolder(
                        stageFolder,
                        zipFile);

                if (string.IsNullOrWhiteSpace(
                    generatedZip))
                {
                    return BuildResult.Fail(
                        "Failed to create the Web site ZIP file.\n\n" +
                        packager.ErrorMessage);
                }

                var packageExe =
                    Assembly.GetExecutingAssembly().Location;

                if (File.Exists(outputFile))
                    File.Delete(outputFile);

                if (requireLicense)
                {
                    var protectedPackager =
                        new ProtectedFilePackager();

                    if (!protectedPackager.PackageFile(
                        outputFile,
                        packageExe,
                        generatedZip,
                        requirement))
                    {
                        return BuildResult.Fail(
                            "Failed to create the protected package.\n\n" +
                            protectedPackager.ErrorMessage);
                    }
                }
                else
                {
                    if (!packager.PackageFile(
                        outputFile,
                        packageExe,
                        generatedZip))
                    {
                        return BuildResult.Fail(
                            "Failed to create the package.\n\n" +
                            packager.ErrorMessage);
                    }
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
                return BuildResult.Fail(
                    ex.GetBaseException().Message);
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
            Directory.CreateDirectory(
                destinationFolder);

            foreach (var file in
                Directory.GetFiles(sourceFolder))
            {
                File.Copy(
                    file,
                    Path.Combine(
                        destinationFolder,
                        Path.GetFileName(file)),
                    true);
            }

            foreach (var directory in
                Directory.GetDirectories(sourceFolder))
            {
                CopyDirectory(
                    directory,
                    Path.Combine(
                        destinationFolder,
                        Path.GetFileName(directory)));
            }
        }

        private static string GetSafeFileName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Packaged";

            var safe = value.Trim();

            foreach (var c in
                Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }

            safe = safe.TrimEnd('.', ' ');

            return string.IsNullOrWhiteSpace(safe)
                ? "Packaged"
                : safe;
        }

        private static string EscapeJsonString(
            string value)
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

        private sealed class BuildResult
        {
            public bool Success
            {
                get;
                private set;
            }

            public string ErrorMessage
            {
                get;
                private set;
            }

            public static BuildResult Ok()
            {
                return new BuildResult
                {
                    Success = true
                };
            }

            public static BuildResult Fail(
                string message)
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
