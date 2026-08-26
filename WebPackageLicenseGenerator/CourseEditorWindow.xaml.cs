using System.Windows;
using WebPackageViewer.CourseCatalog;
using WebPackageViewer.Help;

namespace WebPackageLicenseGenerator
{
    public partial class CourseEditorWindow : Window
    {
        public CourseDefinition Course
        {
            get;
            private set;
        }

        public CourseEditorWindow(
            CourseDefinition existingCourse = null)
        {
            InitializeComponent();
            HelpLauncher.AttachAdministratorHelp(this, "course-catalog");

            if (existingCourse == null)
            {
                Title = "Add Course";
                return;
            }

            Title = "Edit Course";

            ProductCodeTextBox.Text =
                existingCourse.ProductCode;

            ProductCodeTextBox.IsReadOnly = true;

            CourseNameTextBox.Text =
                existingCourse.CourseName;

            CourseVersionTextBox.Text =
                existingCourse.CourseVersion;
        }

        private void SaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var productCode =
                ProductCodeTextBox.Text?.Trim();

            var courseName =
                CourseNameTextBox.Text?.Trim();

            var courseVersion =
                CourseVersionTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(productCode))
            {
                MessageBox.Show(
                    this,
                    "Enter the Salesforce Product Code.",
                    "Course",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                ProductCodeTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(courseName))
            {
                MessageBox.Show(
                    this,
                    "Enter the Course Name.",
                    "Course",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CourseNameTextBox.Focus();
                return;
            }

            Course = new CourseDefinition
            {
                ProductCode = productCode,
                CourseName = courseName,
                CourseVersion =
                    string.IsNullOrWhiteSpace(courseVersion)
                        ? null
                        : courseVersion
            };

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
