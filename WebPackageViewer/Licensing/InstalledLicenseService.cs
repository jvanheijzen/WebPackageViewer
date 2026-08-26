using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WebPackageViewer.Licensing
{
    public sealed class InstalledLicenseService
    {
        public List<InstalledLicenseInfo> GetInstalledLicenses()
        {
            var results =
                new List<InstalledLicenseInfo>();

            AddFolder(
                results,
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "ProgramData");

            AddFolder(
                results,
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "LocalAppData");

            return results
                .OrderBy(x => x.CourseName ?? x.CourseId)
                .ThenBy(x => x.Storage)
                .ToList();
        }

        public void Disable(
            InstalledLicenseInfo license)
        {
            if (license == null)
                throw new ArgumentNullException(nameof(license));

            if (!File.Exists(license.FilePath))
                throw new FileNotFoundException(
                    "The selected license file no longer exists.",
                    license.FilePath);

            if (license.IsDisabled)
                return;

            var destination =
                license.FilePath +
                ".disabled";

            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(
                license.FilePath,
                destination);
        }

        public void Enable(
            InstalledLicenseInfo license)
        {
            if (license == null)
                throw new ArgumentNullException(nameof(license));

            if (!File.Exists(license.FilePath))
                throw new FileNotFoundException(
                    "The selected license file no longer exists.",
                    license.FilePath);

            if (!license.IsDisabled)
                return;

            const string suffix =
                ".disabled";

            if (!license.FilePath.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The selected file is not a disabled license.");
            }

            var destination =
                license.FilePath.Substring(
                    0,
                    license.FilePath.Length -
                    suffix.Length);

            if (File.Exists(destination))
                File.Delete(destination);

            File.Move(
                license.FilePath,
                destination);
        }

        public void Delete(
            InstalledLicenseInfo license)
        {
            if (license == null)
                throw new ArgumentNullException(nameof(license));

            if (File.Exists(license.FilePath))
                File.Delete(license.FilePath);
        }

        private static void AddFolder(
            List<InstalledLicenseInfo> results,
            string baseFolder,
            string storageName)
        {
            var folder =
                Path.Combine(
                    baseFolder,
                    "WebPackageViewer",
                    "Licenses");

            if (!Directory.Exists(folder))
                return;

            foreach (var path in
                Directory.GetFiles(
                    folder,
                    "*.wpl*",
                    SearchOption.TopDirectoryOnly))
            {
                if (!path.EndsWith(
                        ".wpl",
                        StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(
                        ".wpl.disabled",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var disabled =
                    path.EndsWith(
                        ".disabled",
                        StringComparison.OrdinalIgnoreCase);

                var info =
                    new InstalledLicenseInfo
                    {
                        FilePath = path,
                        Storage = storageName,
                        IsDisabled = disabled,
                        Status =
                            disabled
                                ? "Disabled"
                                : "Active"
                    };

                try
                {
                    var file =
                        OfflineLicenseSerializer.ReadLicenseFile(
                            path);

                    var payloadBytes =
                        Convert.FromBase64String(
                            file.PayloadBase64);

                    var payload =
                        OfflineLicenseSerializer.DeserializePayload(
                            payloadBytes);

                    info.CourseId =
                        payload.CourseId;

                    info.CourseName =
                        payload.CourseName;

                    info.Distributor =
                        payload.Distributor;

                    info.MachineId =
                        payload.MachineId;

                    info.Expires =
                        GetExpirationText(
                            payload.ExpiresUtc);

                    if (!disabled)
                    {
                        var requirement =
                            new OfflineLicenseRequirement
                            {
                                CourseId =
                                    payload.CourseId,
                                CourseName =
                                    payload.CourseName
                            };

                        var validation =
                            OfflineLicenseManager.ValidateLicenseFile(
                                path,
                                requirement);

                        info.Status =
                            validation.IsValid
                                ? "Active"
                                : "Invalid: " +
                                  validation.ErrorMessage;
                    }
                }
                catch
                {
                    var filename =
                        Path.GetFileName(path);

                    if (filename.EndsWith(
                        ".disabled",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        filename =
                            filename.Substring(
                                0,
                                filename.Length -
                                ".disabled".Length);
                    }

                    info.CourseId =
                        Path.GetFileNameWithoutExtension(
                            filename);

                    info.Status =
                        disabled
                            ? "Disabled"
                            : "Unreadable";
                }

                results.Add(info);
            }
        }

        private static string GetExpirationText(
            string expiresUtc)
        {
            if (string.IsNullOrWhiteSpace(expiresUtc))
                return "Never";

            DateTime value;

            if (!DateTime.TryParse(
                expiresUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out value))
            {
                return "Invalid";
            }

            return value
                .ToLocalTime()
                .ToString("d");
        }
    }
}
