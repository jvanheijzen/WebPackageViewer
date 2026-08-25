using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace WebPackageViewer.CourseCatalog
{
    public sealed class CourseCatalogService
    {
        private readonly string _catalogPath;

        public CourseCatalogService()
        {
            var baseFolder = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

            var folder = Path.Combine(baseFolder, "WebPackageViewer");
            Directory.CreateDirectory(folder);

            _catalogPath = Path.Combine(folder, "CourseCatalog.json");
        }

        public string CatalogPath => _catalogPath;

        public List<CourseDefinition> Load()
        {
            if (!File.Exists(_catalogPath))
                return new List<CourseDefinition>();

            return LoadFromFile(_catalogPath);
        }

        public void Save(IEnumerable<CourseDefinition> courses)
        {
            SaveToFile(_catalogPath, courses);
        }

        public void Add(CourseDefinition course)
        {
            ValidateCourse(course);

            var courses = Load();

            if (courses.Any(c =>
                string.Equals(
                    c.ProductCode?.Trim(),
                    course.ProductCode.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "A course with that Product Code already exists.");
            }

            Normalize(course);
            courses.Add(course);
            Save(courses);
        }

        public void Update(
            string originalProductCode,
            CourseDefinition course)
        {
            if (string.IsNullOrWhiteSpace(originalProductCode))
            {
                throw new InvalidOperationException(
                    "The original Product Code is required.");
            }

            ValidateCourse(course);

            var courses = Load();

            var existing = courses.FirstOrDefault(c =>
                string.Equals(
                    c.ProductCode,
                    originalProductCode,
                    StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                throw new InvalidOperationException(
                    "The course could not be found in the catalog.");
            }

            if (!string.Equals(
                originalProductCode.Trim(),
                course.ProductCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The Salesforce Product Code cannot be changed for an existing course because it is the license identity.");
            }

            existing.CourseName = course.CourseName;
            existing.CourseVersion = course.CourseVersion;

            Normalize(existing);
            Save(courses);
        }

        public void Delete(string productCode)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return;

            var courses = Load();

            courses.RemoveAll(c =>
                string.Equals(
                    c.ProductCode,
                    productCode,
                    StringComparison.OrdinalIgnoreCase));

            Save(courses);
        }

        public void ExportToFile(string filename)
        {
            SaveToFile(filename, Load());
        }

        public void ImportReplaceFromFile(string filename)
        {
            var imported = LoadFromFile(filename);

            var duplicates = imported
                .GroupBy(
                    c => c.ProductCode,
                    StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException(
                    "The imported catalog contains duplicate Product Codes: " +
                    string.Join(", ", duplicates));
            }

            foreach (var course in imported)
            {
                ValidateCourse(course);
                Normalize(course);
            }

            Save(imported);
        }

        private static List<CourseDefinition> LoadFromFile(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                var serializer =
                    new DataContractJsonSerializer(
                        typeof(List<CourseDefinition>));

                return (List<CourseDefinition>)
                    serializer.ReadObject(stream)
                    ?? new List<CourseDefinition>();
            }
        }

        private static void SaveToFile(
            string filename,
            IEnumerable<CourseDefinition> courses)
        {
            var ordered = courses
                .Where(c => c != null)
                .OrderBy(c => c.CourseName)
                .ThenBy(c => c.CourseVersion)
                .ThenBy(c => c.ProductCode)
                .ToList();

            var folder = Path.GetDirectoryName(filename);

            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            using (var stream = File.Create(filename))
            {
                var serializer =
                    new DataContractJsonSerializer(
                        typeof(List<CourseDefinition>));

                serializer.WriteObject(stream, ordered);
            }
        }

        private static void ValidateCourse(CourseDefinition course)
        {
            if (course == null)
                throw new ArgumentNullException(nameof(course));

            if (string.IsNullOrWhiteSpace(course.ProductCode))
            {
                throw new InvalidOperationException(
                    "Salesforce Product Code is required.");
            }

            if (string.IsNullOrWhiteSpace(course.CourseName))
            {
                throw new InvalidOperationException(
                    "Course Name is required.");
            }
        }

        private static void Normalize(CourseDefinition course)
        {
            course.ProductCode = course.ProductCode.Trim();
            course.CourseName = course.CourseName.Trim();

            course.CourseVersion =
                string.IsNullOrWhiteSpace(course.CourseVersion)
                    ? null
                    : course.CourseVersion.Trim();
        }
    }
}
