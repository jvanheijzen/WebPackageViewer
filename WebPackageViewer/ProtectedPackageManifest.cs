using System.Runtime.Serialization;
using WebPackageViewer.Licensing;

namespace WebPackageViewer
{
    [DataContract]
    public sealed class ProtectedPackageManifest
    {
        [DataMember(Order = 1)]
        public int Version { get; set; } = 1;

        [DataMember(Order = 2)]
        public string CourseId { get; set; }

        [DataMember(Order = 3)]
        public string CourseName { get; set; }

        [DataMember(Order = 4)]
        public string CourseVersion { get; set; }

        [DataMember(Order = 5)]
        public string ModuleId { get; set; }

        [DataMember(Order = 6)]
        public string ModuleName { get; set; }

        public OfflineLicenseRequirement ToLicenseRequirement()
        {
            return new OfflineLicenseRequirement
            {
                Version = Version,
                CourseId = CourseId,
                CourseName = CourseName,
                CourseVersion = CourseVersion,
                ModuleId = ModuleId,
                ModuleName = ModuleName
            };
        }

        public static ProtectedPackageManifest FromRequirement(
            OfflineLicenseRequirement requirement)
        {
            return new ProtectedPackageManifest
            {
                Version = requirement.Version,
                CourseId = requirement.CourseId,
                CourseName = requirement.CourseName,
                CourseVersion = requirement.CourseVersion,
                ModuleId = requirement.ModuleId,
                ModuleName = requirement.ModuleName
            };
        }
    }
}
