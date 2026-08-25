using System.Runtime.Serialization;

namespace WebPackageViewer.Licensing
{
    [DataContract]
    public sealed class OfflineLicenseRequirement
    {
        [DataMember(Order = 1)]
        public int Version { get; set; } = 1;

        // Stable license identity. Use Salesforce Product Code here.
        [DataMember(Order = 2)]
        public string CourseId { get; set; }

        [DataMember(Order = 3)]
        public string CourseName { get; set; }

        // Descriptive package metadata. License validation does NOT
        // currently restrict by course version.
        [DataMember(Order = 4)]
        public string CourseVersion { get; set; }

        [DataMember(Order = 5)]
        public string ModuleId { get; set; }

        [DataMember(Order = 6)]
        public string ModuleName { get; set; }
    }

    [DataContract]
    public sealed class OfflineLicensePayload
    {
        [DataMember(Order = 1)]
        public int Version { get; set; } = 1;

        // License applies to the course, not an individual module.
        [DataMember(Order = 2)]
        public string CourseId { get; set; }

        [DataMember(Order = 3)]
        public string CourseName { get; set; }

        [DataMember(Order = 4)]
        public string Distributor { get; set; }

        [DataMember(Order = 5)]
        public string MachineId { get; set; }

        [DataMember(Order = 6)]
        public string ExpiresUtc { get; set; }
    }

    [DataContract]
    public sealed class OfflineLicenseFile
    {
        [DataMember(Order = 1)]
        public string PayloadBase64 { get; set; }

        [DataMember(Order = 2)]
        public string SignatureBase64 { get; set; }
    }

    public sealed class LicenseValidationResult
    {
        public bool IsValid { get; private set; }

        public string ErrorMessage { get; private set; }

        public OfflineLicensePayload License { get; private set; }

        public static LicenseValidationResult Valid(
            OfflineLicensePayload license)
        {
            return new LicenseValidationResult
            {
                IsValid = true,
                License = license
            };
        }

        public static LicenseValidationResult Invalid(
            string message)
        {
            return new LicenseValidationResult
            {
                IsValid = false,
                ErrorMessage = message
            };
        }
    }
}
