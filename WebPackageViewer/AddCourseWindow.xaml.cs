using System.Windows;
using WebPackageViewer.CourseCatalog;

namespace WebPackageViewer
{
    public partial class AddCourseWindow : Window
    {
        public CourseDefinition Course { get; private set; }

        public AddCourseWindow()
        {
            InitializeComponent();
        }

        private void AddButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var productCode = ProductCodeTextBox.Text?.Trim();
            var courseName = CourseNameTextBox.Text?.Trim();
            var courseVersion = CourseVersionTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(productCode))
            {
                MessageBox.Show(
                    this,
                    "Enter the Salesforce Product Code.",
                    "Add Course",
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
                    "Add Course",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                CourseNameTextBox.Focus();
                return;
            }

            Course = new CourseDefinition
            {
                ProductCode = productCode,
                CourseName = courseName,
                CourseVersion = string.IsNullOrWhiteSpace(courseVersion)
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
