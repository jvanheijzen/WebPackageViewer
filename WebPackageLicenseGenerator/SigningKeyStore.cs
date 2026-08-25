using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WebPackageLicenseGenerator
{
    public static class SigningKeyStore
    {
        public static string KeyFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WebPackageViewer", "LicenseGenerator");

        public static string ProtectedPrivateKeyPath =>
            Path.Combine(KeyFolder, "PrivateKey.protected");

        public static string PublicKeyPath =>
            Path.Combine(KeyFolder, "PublicKey.xml");

        public static bool HasPrivateKey => File.Exists(ProtectedPrivateKeyPath);

        public static string LoadPrivateKeyXml()
        {
            if (!HasPrivateKey)
                throw new InvalidOperationException(
                    "Signing key not initialized. Run Tools\\Initialize-OfflineLicenseKeys.ps1.");

            var protectedBytes = File.ReadAllBytes(ProtectedPrivateKeyPath);
            var privateBytes = ProtectedData.Unprotect(
                protectedBytes, null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(privateBytes);
        }
    }
}
