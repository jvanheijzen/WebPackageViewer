using System.IO;
using System.Runtime.Serialization.Json;

namespace WebPackageViewer.Licensing
{
    public static class OfflineLicenseSerializer
    {
        public static byte[] SerializePayload(OfflineLicensePayload payload) => Serialize(payload);

        public static OfflineLicensePayload DeserializePayload(byte[] bytes) =>
            Deserialize<OfflineLicensePayload>(bytes);

        public static OfflineLicenseRequirement ReadRequirement(string filename) =>
            Deserialize<OfflineLicenseRequirement>(File.ReadAllBytes(filename));

        public static void WriteRequirement(string filename, OfflineLicenseRequirement requirement) =>
            File.WriteAllBytes(filename, Serialize(requirement));

        public static OfflineLicenseFile ReadLicenseFile(string filename) =>
            Deserialize<OfflineLicenseFile>(File.ReadAllBytes(filename));

        public static void WriteLicenseFile(string filename, OfflineLicenseFile licenseFile) =>
            File.WriteAllBytes(filename, Serialize(licenseFile));

        private static byte[] Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                serializer.WriteObject(stream, value);
                return stream.ToArray();
            }
        }

        private static T Deserialize<T>(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                return (T)serializer.ReadObject(stream);
            }
        }
    }
}
