using Microsoft.Win32;
using System;
using System.Security.Cryptography;
using System.Text;

namespace WebPackageViewer.Licensing
{
    public static class MachineIdentity
    {
        public static string GetMachineId()
        {
            var machineGuid = ReadMachineGuid();

            if (string.IsNullOrWhiteSpace(machineGuid))
                machineGuid = Environment.MachineName;

            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(machineGuid.Trim().ToUpperInvariant());
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(32);

                for (var i = 0; i < 16; i++)
                    sb.Append(hash[i].ToString("X2"));

                return sb.ToString();
            }
        }

        public static string GetDisplayMachineId()
        {
            var id = GetMachineId();
            var sb = new StringBuilder();

            for (var i = 0; i < id.Length; i += 4)
            {
                if (sb.Length > 0)
                    sb.Append('-');

                sb.Append(id.Substring(i, Math.Min(4, id.Length - i)));
            }

            return sb.ToString();
        }

        public static string NormalizeMachineId(string machineId)
        {
            if (string.IsNullOrWhiteSpace(machineId))
                return string.Empty;

            return machineId.Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Trim()
                .ToUpperInvariant();
        }

        private static string ReadMachineGuid()
        {
            var value = ReadMachineGuid(RegistryView.Registry64);

            if (!string.IsNullOrWhiteSpace(value))
                return value;

            return ReadMachineGuid(RegistryView.Registry32);
        }

        private static string ReadMachineGuid(RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false))
                {
                    return key?.GetValue("MachineGuid") as string;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
