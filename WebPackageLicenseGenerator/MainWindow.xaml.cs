using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WebPackageViewer.CourseCatalog;

namespace WebPackageLicenseGenerator
{
    public partial class MainWindow : Window
    {
        private readonly CourseCatalogService _catalog =
            new CourseCatalogService();

        public MainWindow()
        {
            InitializeComponent();

            ExpirationDatePicker.SelectedDate =
                DateTime.Today.AddYears(1);

            LoadCourses();
            RefreshKeyStatus();
        }

        private void LoadCourses(
            string preferredProductCode = null)
        {
            var courses = _catalog.Load();

            CourseComboBox.ItemsSource = courses;

            if (courses.Count == 0)
            {
                ProductCodeTextBox.Text = string.Empty;
                CourseNameTextBox.Text = string.Empty;

                StatusTextBlock.Text =
                    "No courses found. Use Manage Courses to add or import a course catalog.";
                return;
            }

            CourseDefinition selection = null;

            if (!string.IsNullOrWhiteSpace(preferredProductCode))
            {
                foreach (var course in courses)
                {
                    if (string.Equals(
                        course.ProductCode,
                        preferredProductCode,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        selection = course;
                        break;
                    }
                }
            }

            CourseComboBox.SelectedItem =
                selection ?? courses[0];
        }

        private void RefreshKeyStatus()
        {
            KeyStatusTextBlock.Text =
                SigningKeyStore.HasPrivateKey
                    ? "Signing key initialized. Create a portable recovery backup before moving to another computer or issuing production licenses."
                    : "Signing key not found. Restore the existing signing identity before generating licenses.";
        }

        private void ManageCoursesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var current =
                CourseComboBox.SelectedItem as CourseDefinition;

            var dialog =
                new ManageCoursesWindow
                {
                    Owner = this
                };

            dialog.ShowDialog();
            LoadCourses(current?.ProductCode);
        }

        private void CourseComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            var course =
                CourseComboBox.SelectedItem as CourseDefinition;

            ProductCodeTextBox.Text =
                course?.ProductCode ?? string.Empty;

            CourseNameTextBox.Text =
                course?.CourseName ?? string.Empty;
        }

        private void NeverExpiresCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (ExpirationDatePicker != null)
            {
                ExpirationDatePicker.IsEnabled =
                    NeverExpiresCheckBox.IsChecked != true;
            }
        }

        private void ExportRecoveryBackupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (!SigningKeyStore.HasPrivateKey)
                {
                    MessageBox.Show(
                        this,
                        "There is no signing key to back up.",
                        "Signing Key Backup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var passwordDialog =
                    new BackupPasswordWindow(
                        requireConfirmation: true)
                    {
                        Owner = this
                    };

                if (passwordDialog.ShowDialog() != true)
                    return;

                var saveDialog =
                    new SaveFileDialog
                    {
                        Title =
                            "Save Signing Key Recovery Backup",

                        Filter =
                            "Web Package signing-key backup (*.wpkey)|*.wpkey|" +
                            "All files (*.*)|*.*",

                        AddExtension = true,
                        DefaultExt = ".wpkey",
                        FileName =
                            "WebPackageViewer-SigningKey-Recovery.wpkey"
                    };

                if (saveDialog.ShowDialog(this) != true)
                    return;

                SigningKeyBackupService.ExportBackup(
                    saveDialog.FileName,
                    passwordDialog.BackupPassword);

                MessageBox.Show(
                    this,
                    "Recovery backup created successfully.\n\n" +
                    saveDialog.FileName +
                    "\n\nStore the backup file and password separately.",
                    "Signing Key Backup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Signing Key Backup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RestoreRecoveryBackupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (SigningKeyStore.HasPrivateKey)
                {
                    var answer =
                        MessageBox.Show(
                            this,
                            "A signing key is already installed for this Windows user.\n\n" +
                            "Continue only if the selected backup contains the same intended signing identity.",
                            "Restore Signing Key",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                    if (answer != MessageBoxResult.Yes)
                        return;
                }

                var openDialog =
                    new OpenFileDialog
                    {
                        Title =
                            "Open Signing Key Recovery Backup",

                        Filter =
                            "Web Package signing-key backup (*.wpkey)|*.wpkey|" +
                            "All files (*.*)|*.*",

                        CheckFileExists = true,
                        Multiselect = false
                    };

                if (openDialog.ShowDialog(this) != true)
                    return;

                var passwordDialog =
                    new BackupPasswordWindow(
                        requireConfirmation: false)
                    {
                        Owner = this
                    };

                if (passwordDialog.ShowDialog() != true)
                    return;

                SigningKeyBackupService.RestoreBackup(
                    openDialog.FileName,
                    passwordDialog.BackupPassword);

                RefreshKeyStatus();

                MessageBox.Show(
                    this,
                    "Signing key restored successfully for this Windows user.\n\n" +
                    "Use the same Git branch/public key when rebuilding WebPackageViewer.",
                    "Restore Signing Key",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Restore Signing Key",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void GenerateLicenseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                var course =
                    CourseComboBox.SelectedItem as CourseDefinition;

                if (course == null)
                {
                    MessageBox.Show(
                        this,
                        "Select a course.",
                        "License Generator",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!SigningKeyStore.HasPrivateKey)
                {
                    MessageBox.Show(
                        this,
                        "The signing key is not available. Restore the existing signing identity first.",
                        "License Generator",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var distributor =
                    DistributorTextBox.Text?.Trim();

                if (string.IsNullOrWhiteSpace(distributor))
                {
                    MessageBox.Show(
                        this,
                        "Enter the distributor name.",
                        "License Generator",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                DateTime? expiresUtc = null;

                if (NeverExpiresCheckBox.IsChecked != true)
                {
                    if (!ExpirationDatePicker.SelectedDate.HasValue)
                    {
                        MessageBox.Show(
                            this,
                            "Choose an expiration date.",
                            "License Generator",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    expiresUtc =
                        ExpirationDatePicker.SelectedDate.Value
                            .Date
                            .AddDays(1)
                            .AddTicks(-1)
                            .ToUniversalTime();
                }

                var dialog =
                    new SaveFileDialog
                    {
                        Title =
                            "Save Offline Course License",

                        Filter =
                            "Web Package license (*.wpl)|*.wpl|" +
                            "All files (*.*)|*.*",

                        AddExtension = true,
                        DefaultExt = ".wpl",

                        FileName =
                            GetSafeFileName(distributor) +
                            "-" +
                            GetSafeFileName(course.ProductCode) +
                            ".wpl"
                    };

                if (dialog.ShowDialog(this) != true)
                    return;

                LicenseGeneratorService.Generate(
                    dialog.FileName,
                    course,
                    distributor,
                    MachineIdTextBox.Text?.Trim(),
                    expiresUtc);

                StatusTextBlock.Text =
                    "License generated successfully.";

                MessageBox.Show(
                    this,
                    "License created:\n\n" +
                    dialog.FileName +
                    "\n\nIt will unlock every module using Product Code " +
                    course.ProductCode +
                    " on that computer.",
                    "License Generator",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text =
                    "License generation failed.";

                MessageBox.Show(
                    this,
                    ex.Message,
                    "License Generator",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string GetSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "license";

            var safe = value.Trim();

            foreach (var c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');

            return safe.TrimEnd('.', ' ');
        }
    }
}
