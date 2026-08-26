using System.Windows;
using WebPackageViewer.Help;

namespace WebPackageLicenseGenerator
{
    public partial class BackupPasswordWindow : Window
    {
        private readonly bool _requireConfirmation;

        public string BackupPassword
        {
            get;
            private set;
        }

        public BackupPasswordWindow(bool requireConfirmation)
        {
            InitializeComponent();
            HelpLauncher.AttachAdministratorHelp(this, "signing-backup");

            _requireConfirmation = requireConfirmation;

            if (requireConfirmation)
            {
                Title =
                    "Create Signing Key Recovery Backup";

                InstructionTextBlock.Text =
                    "Choose a strong password for the portable recovery backup. " +
                    "You will need both the backup file and this password to restore the signing identity on another Windows installation.";
            }
            else
            {
                Title =
                    "Restore Signing Key Recovery Backup";

                InstructionTextBlock.Text =
                    "Enter the password used when the signing-key recovery backup was created.";

                ConfirmationPanel.Visibility =
                    Visibility.Collapsed;
            }
        }

        private void ContinueButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(password))
            {
                StatusTextBlock.Text =
                    "Enter the backup password.";
                return;
            }

            if (_requireConfirmation)
            {
                if (password.Length < 12)
                {
                    StatusTextBlock.Text =
                        "Use a password of at least 12 characters.";
                    return;
                }

                if (password != ConfirmPasswordBox.Password)
                {
                    StatusTextBlock.Text =
                        "The passwords do not match.";
                    return;
                }
            }

            BackupPassword = password;
            DialogResult = true;
        }

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
