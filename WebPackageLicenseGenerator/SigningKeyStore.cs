using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WebPackageLicenseGenerator
{
    public static class SigningKeyStore
    {
        public static string KeyFolder => Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "WebPackageViewer",
            "LicenseGenerator");

        public static string ProtectedPrivateKeyPath =>
            Path.Combine(KeyFolder, "PrivateKey.protected");

        public static string PublicKeyPath =>
            Path.Combine(KeyFolder, "PublicKey.xml");

        public static bool HasPrivateKey =>
            File.Exists(ProtectedPrivateKeyPath);

        public static string LoadPrivateKeyXml()
        {
            if (!HasPrivateKey)
            {
                throw new InvalidOperationException(
                    "Signing key not initialized.");
            }

            var protectedBytes =
                File.ReadAllBytes(ProtectedPrivateKeyPath);

            var privateBytes =
                ProtectedData.Unprotect(
                    protectedBytes,
                    null,
                    DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(privateBytes);
        }

        public static string LoadPublicKeyXml()
        {
            if (!File.Exists(PublicKeyPath))
                return null;

            return File.ReadAllText(PublicKeyPath).Trim();
        }

        public static void InstallPrivateKeyXml(string privateKeyXml)
        {
            if (string.IsNullOrWhiteSpace(privateKeyXml))
            {
                throw new InvalidOperationException(
                    "The private key is empty.");
            }

            string publicKeyXml;

            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(privateKeyXml);

                var testData =
                    Encoding.UTF8.GetBytes(
                        "WebPackageViewer signing-key validation");

                rsa.SignData(
                    testData,
                    CryptoConfig.MapNameToOID("SHA256"));

                publicKeyXml = rsa.ToXmlString(false);
            }

            Directory.CreateDirectory(KeyFolder);

            var privateBytes =
                Encoding.UTF8.GetBytes(privateKeyXml);

            var protectedBytes =
                ProtectedData.Protect(
                    privateBytes,
                    null,
                    DataProtectionScope.CurrentUser);

            File.WriteAllBytes(
                ProtectedPrivateKeyPath,
                protectedBytes);

            File.WriteAllText(
                PublicKeyPath,
                publicKeyXml,
                new UTF8Encoding(false));
        }
    }
}
