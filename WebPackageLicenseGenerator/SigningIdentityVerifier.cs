using System;
using WebPackageViewer.Licensing;

namespace WebPackageLicenseGenerator
{
    public static class SigningIdentityVerifier
    {
        public static bool MatchesViewerPublicKey(
            out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (!SigningKeyStore.HasPrivateKey)
                {
                    errorMessage =
                        "No signing identity is installed.";
                    return false;
                }

                var installed =
                    NormalizeXml(
                        SigningKeyStore.LoadPublicKeyXml());

                var compiled =
                    NormalizeXml(
                        LicenseKeys.PublicKeyXml);

                if (string.IsNullOrWhiteSpace(compiled))
                {
                    errorMessage =
                        "The viewer build has no public verification key.";
                    return false;
                }

                if (!string.Equals(
                    installed,
                    compiled,
                    StringComparison.Ordinal))
                {
                    errorMessage =
                        "The installed signing identity does not match the public key compiled into this WebPackageViewer build.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage =
                    ex.GetBaseException().Message;
                return false;
            }
        }

        private static string NormalizeXml(
            string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\t", string.Empty)
                .Replace(" ", string.Empty)
                .Trim();
        }
    }
}
