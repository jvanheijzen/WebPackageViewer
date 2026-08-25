using Microsoft.Win32;
using System;
using System.Windows;
using WebPackageViewer.Licensing;

namespace WebPackageViewer
{
    public partial class LicenseActivationWindow : Window
    {
        private readonly OfflineLicenseRequirement _requirement;

        public LicenseActivationWindow(
            OfflineLicenseRequirement requirement,
            string validationMessage = null)
        {
            InitializeComponent();

            _requirement = requirement;

            CourseTextBox.Text =
                string.IsNullOrWhiteSpace(requirement.CourseName)
                    ? requirement.CourseId
                    : requirement.CourseName + " (" + requirement.CourseId + ")";

            MachineIdTextBox.Text = MachineIdentity.GetDisplayMachineId();

            if (!string.IsNullOrWhiteSpace(validationMessage))
                StatusTextBlock.Text = validationMessage;
        }

        private void CopyMachineIdButton_Click(object sender, RoutedEventArgs e)
        {
            var text =
                "Course ID: " + _requirement.CourseId +
                Environment.NewLine +
                "Machine ID: " + MachineIdentity.GetDisplayMachineId();

            Clipboard.SetText(text);
            StatusTextBlock.Text =
                "Course ID and machine ID copied to the clipboard.";
        }

        private void ImportLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Offline License",
                Filter = "Web Package license (*.wpl)|*.wpl|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
                return;

            var result =
                OfflineLicenseManager.ImportLicense(dialog.FileName, _requirement);

            if (!result.IsValid)
            {
                StatusTextBlock.Text = result.ErrorMessage;
                return;
            }

            MessageBox.Show(
                this,
                "This computer has been activated for offline use.",
                "License Activated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
