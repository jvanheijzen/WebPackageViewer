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

            var folder = Path.Combine(
                baseFolder,
                "WebPackageViewer");

            Directory.CreateDirectory(folder);

            _catalogPath = Path.Combine(
                folder,
                "CourseCatalog.json");
        }

        public string CatalogPath => _catalogPath;

        public List<CourseDefinition> Load()
        {
            if (!File.Exists(_catalogPath))
                return new List<CourseDefinition>();

            try
            {
                using (var stream = File.OpenRead(_catalogPath))
                {
                    var serializer =
                        new DataContractJsonSerializer(
                            typeof(List<CourseDefinition>));

                    return (List<CourseDefinition>)
                        serializer.ReadObject(stream)
                        ?? new List<CourseDefinition>();
                }
            }
            catch
            {
                return new List<CourseDefinition>();
            }
        }

        public void Save(IEnumerable<CourseDefinition> courses)
        {
            var ordered = courses
                .Where(c => c != null)
                .OrderBy(c => c.CourseName)
                .ThenBy(c => c.CourseVersion)
                .ThenBy(c => c.ProductCode)
                .ToList();

            using (var stream = File.Create(_catalogPath))
            {
                var serializer =
                    new DataContractJsonSerializer(
                        typeof(List<CourseDefinition>));

                serializer.WriteObject(stream, ordered);
            }
        }

        public void Add(CourseDefinition course)
        {
            if (course == null)
                throw new ArgumentNullException(nameof(course));

            if (string.IsNullOrWhiteSpace(course.ProductCode))
                throw new InvalidOperationException(
                    "Product Code is required.");

            if (string.IsNullOrWhiteSpace(course.CourseName))
                throw new InvalidOperationException(
                    "Course Name is required.");

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

            course.ProductCode = course.ProductCode.Trim();
            course.CourseName = course.CourseName.Trim();
            course.CourseVersion =
                string.IsNullOrWhiteSpace(course.CourseVersion)
                    ? null
                    : course.CourseVersion.Trim();

            courses.Add(course);
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
    }
}
