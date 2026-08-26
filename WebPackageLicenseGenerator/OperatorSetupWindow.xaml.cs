using Microsoft.Win32;
using System;
using System.Windows;
using WebPackageViewer.CourseCatalog;
using WebPackageViewer.Help;

namespace WebPackageLicenseGenerator
{
    public partial class OperatorSetupWindow : Window
    {
        private readonly CourseCatalogService _catalog =
            new CourseCatalogService();

        public OperatorSetupWindow()
        {
            InitializeComponent();
            RefreshStatus();
            HelpLauncher.AttachAdministratorHelp(this, "operator-setup");
        }

        private void RefreshStatus()
        {
            string verificationError;

            if (SigningIdentityVerifier.MatchesViewerPublicKey(
                out verificationError))
            {
                SigningStatusTextBlock.Text =
                    "Signing identity: Ready — installed key matches this WebPackageViewer build.";
            }
            else if (SigningKeyStore.HasPrivateKey)
            {
                SigningStatusTextBlock.Text =
                    "Signing identity: WARNING — " +
                    verificationError;
            }
            else
            {
                SigningStatusTextBlock.Text =
                    "Signing identity: NOT INSTALLED — restore the existing .wpkey before generating licenses.";
            }

            var courses =
                _catalog.Load();

            CatalogStatusTextBlock.Text =
                "Course catalog: " +
                courses.Count +
                " course(s) available.";
        }

        private void RestoreSigningKeyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (SigningKeyStore.HasPrivateKey)
            {
                var answer =
                    MessageBox.Show(
                        this,
                        "A signing identity is already installed for this Windows user.\n\n" +
                        "Continue only if the recovery backup contains the intended existing signing identity.",
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

            try
            {
                SigningKeyBackupService.RestoreBackup(
                    openDialog.FileName,
                    passwordDialog.BackupPassword);

                RefreshStatus();

                string verificationError;

                if (!SigningIdentityVerifier.MatchesViewerPublicKey(
                    out verificationError))
                {
                    MessageBox.Show(
                        this,
                        "The signing key was restored, but it does not match this viewer build.\n\n" +
                        verificationError +
                        "\n\nDo not generate production licenses until this is corrected.",
                        "Signing Identity Mismatch",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.GetBaseException().Message,
                    "Restore Signing Key",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ImportCatalogButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog =
                new OpenFileDialog
                {
                    Title =
                        "Import Course Catalog",
                    Filter =
                        "Web Package course catalog (*.wpcourses)|*.wpcourses|" +
                        "JSON files (*.json)|*.json|" +
                        "All files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

            if (dialog.ShowDialog(this) != true)
                return;

            var answer =
                MessageBox.Show(
                    this,
                    "Importing replaces the local course catalog on this computer.\n\nContinue?",
                    "Import Course Catalog",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                _catalog.ImportReplaceFromFile(
                    dialog.FileName);

                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.GetBaseException().Message,
                    "Import Course Catalog",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshStatus();
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}
