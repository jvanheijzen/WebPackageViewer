using Microsoft.Win32;
using System;
using System.Windows;
using WebPackageViewer.CourseCatalog;
using WebPackageViewer.Help;

namespace WebPackageLicenseGenerator
{
    public partial class ManageCoursesWindow : Window
    {
        private readonly CourseCatalogService _catalog =
            new CourseCatalogService();

        public ManageCoursesWindow()
        {
            InitializeComponent();
            Reload();
            HelpLauncher.AttachAdministratorHelp(this, "course-catalog");
        }

        private void Reload(string selectProductCode = null)
        {
            var courses = _catalog.Load();
            CoursesDataGrid.ItemsSource = courses;

            if (!string.IsNullOrWhiteSpace(selectProductCode))
            {
                foreach (var course in courses)
                {
                    if (string.Equals(
                        course.ProductCode,
                        selectProductCode,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        CoursesDataGrid.SelectedItem = course;
                        break;
                    }
                }
            }
        }

        private void AddButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new CourseEditorWindow
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _catalog.Add(dialog.Course);
                Reload(dialog.Course.ProductCode);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void EditButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var selected =
                CoursesDataGrid.SelectedItem as CourseDefinition;

            if (selected == null)
            {
                MessageBox.Show(
                    this,
                    "Select a course to edit.",
                    "Manage Courses",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog =
                new CourseEditorWindow(selected)
                {
                    Owner = this
                };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                _catalog.Update(
                    selected.ProductCode,
                    dialog.Course);

                Reload(selected.ProductCode);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void DeleteButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var selected =
                CoursesDataGrid.SelectedItem as CourseDefinition;

            if (selected == null)
            {
                MessageBox.Show(
                    this,
                    "Select a course to delete.",
                    "Manage Courses",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var answer =
                MessageBox.Show(
                    this,
                    "Remove this course from the local catalog?\n\n" +
                    selected.CourseName +
                    "\n" +
                    selected.ProductCode +
                    "\n\nExisting packaged modules and previously issued licenses are not changed.",
                    "Delete Course",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            _catalog.Delete(selected.ProductCode);
            Reload();
        }

        private void ExportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog =
                new SaveFileDialog
                {
                    Title =
                        "Export Course Catalog",

                    Filter =
                        "Web Package course catalog (*.wpcourses)|*.wpcourses|" +
                        "JSON files (*.json)|*.json|" +
                        "All files (*.*)|*.*",

                    AddExtension = true,
                    DefaultExt = ".wpcourses",
                    FileName =
                        "WebPackageViewer-CourseCatalog.wpcourses"
                };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                _catalog.ExportToFile(dialog.FileName);

                MessageBox.Show(
                    this,
                    "Course catalog exported successfully.",
                    "Manage Courses",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void ImportButton_Click(
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
                    "Importing will replace the current local course catalog.\n\nContinue?",
                    "Import Course Catalog",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                _catalog.ImportReplaceFromFile(dialog.FileName);
                Reload();

                MessageBox.Show(
                    this,
                    "Course catalog imported successfully.",
                    "Manage Courses",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void ShowError(string message)
        {
            MessageBox.Show(
                this,
                message,
                "Manage Courses",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
