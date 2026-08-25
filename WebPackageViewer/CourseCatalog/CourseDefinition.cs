using System.Runtime.Serialization;

namespace WebPackageViewer.CourseCatalog
{
    [DataContract]
    public sealed class CourseDefinition
    {
        [DataMember(Order = 1)]
        public string ProductCode { get; set; }

        [DataMember(Order = 2)]
        public string CourseName { get; set; }

        [DataMember(Order = 3)]
        public string CourseVersion { get; set; }

        public string DisplayName
        {
            get
            {
                var version = string.IsNullOrWhiteSpace(CourseVersion)
                    ? string.Empty
                    : " " + CourseVersion;

                return $"{CourseName}{version} [{ProductCode}]";
            }
        }
    }
}
