using System;
using System.Globalization;
using System.Security.Cryptography;
using WebPackageViewer.CourseCatalog;
using WebPackageViewer.Licensing;

namespace WebPackageLicenseGenerator
{
    public static class LicenseGeneratorService
    {
        public static void Generate(
            string outputFile,
            CourseDefinition course,
            string distributor,
            string machineId,
            DateTime? expiresUtc)
        {
            if (course == null)
                throw new ArgumentNullException(nameof(course));

            if (string.IsNullOrWhiteSpace(course.ProductCode))
                throw new InvalidOperationException("Selected course has no Product Code.");

            if (string.IsNullOrWhiteSpace(distributor))
                throw new InvalidOperationException("Distributor is required.");

            var normalized = MachineIdentity.NormalizeMachineId(machineId);

            if (normalized.Length != 32)
                throw new InvalidOperationException(
                    "Machine ID must contain 32 hexadecimal characters.");

            foreach (var c in normalized)
                if (!Uri.IsHexDigit(c))
                    throw new InvalidOperationException(
                        "Machine ID contains invalid characters.");

            var payload = new OfflineLicensePayload
            {
                Version = 1,
                CourseId = course.ProductCode.Trim(),
                CourseName = course.CourseName?.Trim(),
                Distributor = distributor.Trim(),
                MachineId = normalized,
                ExpiresUtc = expiresUtc.HasValue
                    ? expiresUtc.Value.ToUniversalTime()
                        .ToString("o", CultureInfo.InvariantCulture)
                    : null
            };

            var payloadBytes = OfflineLicenseSerializer.SerializePayload(payload);
            byte[] signature;

            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(SigningKeyStore.LoadPrivateKeyXml());
                signature = rsa.SignData(
                    payloadBytes,
                    CryptoConfig.MapNameToOID("SHA256"));
            }

            OfflineLicenseSerializer.WriteLicenseFile(
                outputFile,
                new OfflineLicenseFile
                {
                    PayloadBase64 = Convert.ToBase64String(payloadBytes),
                    SignatureBase64 = Convert.ToBase64String(signature)
                });
        }
    }
}
