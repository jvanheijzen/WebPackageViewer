using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace WebPackageViewer.Licensing
{
    public static class OfflineLicenseManager
    {
        public const string RequirementFileName = "WebPackageViewer.license.json";

        public static OfflineLicenseRequirement FindRequirement(string webRoot)
        {
            if (string.IsNullOrWhiteSpace(webRoot))
                return null;

            var filename = Path.Combine(webRoot, RequirementFileName);

            if (!File.Exists(filename))
                return null;

            try
            {
                var requirement = OfflineLicenseSerializer.ReadRequirement(filename);

                if (requirement == null || string.IsNullOrWhiteSpace(requirement.CourseId))
                    return null;

                return requirement;
            }
            catch
            {
                return null;
            }
        }

        public static LicenseValidationResult ValidateInstalledLicense(
            OfflineLicenseRequirement requirement)
        {
            var licensePath = GetInstalledLicensePath(requirement.CourseId);

            if (!File.Exists(licensePath))
                licensePath = GetInstalledLicensePath(requirement.CourseId, useLocalAppData: true);

            if (!File.Exists(licensePath))
                return LicenseValidationResult.Invalid("This course is not activated on this computer.");

            return ValidateLicenseFile(licensePath, requirement);
        }

        public static LicenseValidationResult ValidateLicenseFile(
            string licensePath,
            OfflineLicenseRequirement requirement)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(LicenseKeys.PublicKeyXml))
                    return LicenseValidationResult.Invalid(
                        "Offline licensing has not been configured with a public verification key.");

                var file = OfflineLicenseSerializer.ReadLicenseFile(licensePath);

                if (file == null ||
                    string.IsNullOrWhiteSpace(file.PayloadBase64) ||
                    string.IsNullOrWhiteSpace(file.SignatureBase64))
                {
                    return LicenseValidationResult.Invalid("The selected license file is invalid.");
                }

                var payloadBytes = Convert.FromBase64String(file.PayloadBase64);
                var signatureBytes = Convert.FromBase64String(file.SignatureBase64);

                if (!VerifySignature(payloadBytes, signatureBytes))
                    return LicenseValidationResult.Invalid(
                        "The selected license file has an invalid signature.");

                var payload = OfflineLicenseSerializer.DeserializePayload(payloadBytes);

                if (!string.Equals(payload.CourseId, requirement.CourseId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return LicenseValidationResult.Invalid(
                        "This license is for a different course.");
                }

                var currentMachine =
                    MachineIdentity.NormalizeMachineId(MachineIdentity.GetMachineId());

                var licensedMachine =
                    MachineIdentity.NormalizeMachineId(payload.MachineId);

                if (!string.Equals(currentMachine, licensedMachine,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return LicenseValidationResult.Invalid(
                        "This license is for a different computer.");
                }

                if (!string.IsNullOrWhiteSpace(payload.ExpiresUtc))
                {
                    DateTime expiresUtc;

                    if (!DateTime.TryParse(payload.ExpiresUtc,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out expiresUtc))
                    {
                        return LicenseValidationResult.Invalid(
                            "The license expiration date is invalid.");
                    }

                    if (DateTime.UtcNow > expiresUtc)
                        return LicenseValidationResult.Invalid(
                            "This offline license has expired.");
                }

                return LicenseValidationResult.Valid(payload);
            }
            catch (Exception ex)
            {
                return LicenseValidationResult.Invalid(
                    "The license could not be read: " + ex.Message);
            }
        }

        public static LicenseValidationResult ImportLicense(
            string sourceLicensePath,
            OfflineLicenseRequirement requirement)
        {
            var result = ValidateLicenseFile(sourceLicensePath, requirement);

            if (!result.IsValid)
                return result;

            try
            {
                var destination = GetInstalledLicensePath(requirement.CourseId);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(sourceLicensePath, destination, overwrite: true);
                }
                catch (UnauthorizedAccessException)
                {
                    destination = GetInstalledLicensePath(
                        requirement.CourseId, useLocalAppData: true);

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(sourceLicensePath, destination, overwrite: true);
                }

                return result;
            }
            catch (Exception ex)
            {
                return LicenseValidationResult.Invalid(
                    "The license is valid, but it could not be installed: " + ex.Message);
            }
        }

        public static string GetInstalledLicensePath(
            string courseId,
            bool useLocalAppData = false)
        {
            var baseFolder = Environment.GetFolderPath(
                useLocalAppData
                    ? Environment.SpecialFolder.LocalApplicationData
                    : Environment.SpecialFolder.CommonApplicationData);

            return Path.Combine(
                baseFolder,
                "WebPackageViewer",
                "Licenses",
                GetSafeFileName(courseId) + ".wpl");
        }

        private static bool VerifySignature(byte[] payloadBytes, byte[] signatureBytes)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(LicenseKeys.PublicKeyXml);

                return rsa.VerifyData(
                    payloadBytes,
                    CryptoConfig.MapNameToOID("SHA256"),
                    signatureBytes);
            }
        }

        private static string GetSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "license";

            var safe = value.Trim();

            foreach (var c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');

            return safe;
        }
    }
}
