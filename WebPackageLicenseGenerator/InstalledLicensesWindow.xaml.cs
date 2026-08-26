using System;
using System.Windows;
using WebPackageViewer.Licensing;
using WebPackageViewer.Help;

namespace WebPackageLicenseGenerator
{
    public partial class InstalledLicensesWindow : Window
    {
        private readonly InstalledLicenseService _service =
            new InstalledLicenseService();

        public InstalledLicensesWindow()
        {
            InitializeComponent();
            Reload();
            HelpLauncher.AttachAdministratorHelp(this, "installed-licenses");
        }

        private InstalledLicenseInfo Selected =>
            LicensesDataGrid.SelectedItem
            as InstalledLicenseInfo;

        private void Reload()
        {
            LicensesDataGrid.ItemsSource =
                _service.GetInstalledLicenses();
        }

        private void DisableButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Selected == null)
                return;

            try
            {
                _service.Disable(Selected);
                Reload();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void EnableButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Selected == null)
                return;

            try
            {
                _service.Enable(Selected);
                Reload();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void DeleteButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Selected == null)
                return;

            var answer =
                MessageBox.Show(
                    this,
                    "Permanently delete this local course license?\n\n" +
                    (Selected.CourseName ??
                     Selected.CourseId) +
                    "\n\nThis computer will require activation again.",
                    "Delete Local License",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                _service.Delete(Selected);
                Reload();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Reload();
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void ShowError(Exception ex)
        {
            MessageBox.Show(
                this,
                ex.GetBaseException().Message,
                "Installed Licenses",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
