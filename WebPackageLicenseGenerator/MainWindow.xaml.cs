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
        private readonly CourseCatalogService _catalog = new CourseCatalogService();

        public MainWindow()
        {
            InitializeComponent();
            ExpirationDatePicker.SelectedDate = DateTime.Today.AddYears(1);
            LoadCourses();
            KeyStatusTextBlock.Text = SigningKeyStore.HasPrivateKey
                ? "Signing key initialized for this Windows user."
                : "Signing key not found. Run Tools\\Initialize-OfflineLicenseKeys.ps1.";
        }

        private void LoadCourses()
        {
            var courses = _catalog.Load();
            CourseComboBox.ItemsSource = courses;

            if (courses.Count > 0)
                CourseComboBox.SelectedIndex = 0;
            else
                StatusTextBlock.Text =
                    "No courses found. Add the course in Web Package Builder first.";
        }

        private void CourseComboBox_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            var course = CourseComboBox.SelectedItem as CourseDefinition;
            ProductCodeTextBox.Text = course?.ProductCode ?? string.Empty;
            CourseNameTextBox.Text = course?.CourseName ?? string.Empty;
        }

        private void NeverExpiresCheckBox_Changed(
            object sender, RoutedEventArgs e)
        {
            if (ExpirationDatePicker != null)
                ExpirationDatePicker.IsEnabled =
                    NeverExpiresCheckBox.IsChecked != true;
        }

        private void GenerateLicenseButton_Click(
            object sender, RoutedEventArgs e)
        {
            try
            {
                var course = CourseComboBox.SelectedItem as CourseDefinition;

                if (course == null)
                {
                    MessageBox.Show(this, "Select a course.", "License Generator",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!SigningKeyStore.HasPrivateKey)
                {
                    MessageBox.Show(this,
                        "Run Tools\\Initialize-OfflineLicenseKeys.ps1 first.",
                        "License Generator",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var distributor = DistributorTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(distributor))
                {
                    MessageBox.Show(this, "Enter the distributor name.",
                        "License Generator", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                DateTime? expiresUtc = null;

                if (NeverExpiresCheckBox.IsChecked != true)
                {
                    if (!ExpirationDatePicker.SelectedDate.HasValue)
                    {
                        MessageBox.Show(this, "Choose an expiration date.",
                            "License Generator", MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    expiresUtc = ExpirationDatePicker.SelectedDate.Value
                        .Date.AddDays(1).AddTicks(-1).ToUniversalTime();
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Save Offline Course License",
                    Filter = "Web Package license (*.wpl)|*.wpl|All files (*.*)|*.*",
                    AddExtension = true,
                    DefaultExt = ".wpl",
                    FileName = GetSafeFileName(distributor) + "-" +
                               GetSafeFileName(course.ProductCode) + ".wpl"
                };

                if (dialog.ShowDialog(this) != true)
                    return;

                LicenseGeneratorService.Generate(
                    dialog.FileName,
                    course,
                    distributor,
                    MachineIdTextBox.Text?.Trim(),
                    expiresUtc);

                StatusTextBlock.Text = "License generated successfully.";

                MessageBox.Show(this,
                    "License created:\n\n" + dialog.FileName +
                    "\n\nIt will unlock every module using Product Code " +
                    course.ProductCode + " on that computer.",
                    "License Generator",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "License generation failed.";
                MessageBox.Show(this, ex.Message, "License Generator",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
