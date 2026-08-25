using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using WebPackageViewer.Licensing;

namespace WebPackageViewer
{
    /// <summary>
    /// Packages the appended Web site ZIP in an encrypted/authenticated envelope.
    ///
    /// This is intended as a practical deterrent against casual extraction.
    /// The payload-protection key is derived from public data compiled into the
    /// viewer, so this is not intended to defeat determined reverse engineering.
    /// The signed, machine-bound offline license remains the authorization control.
    /// </summary>
    public sealed class ProtectedFilePackager
    {
        public const string ProtectedSeparatorString =
            "--- WPV PROTECTED PACKAGE V1 ---";

        private const int IvLength = 16;
        private const int MacLength = 32;
        private const int MaximumManifestLength = 64 * 1024;

        public byte[] ProtectedSeparatorBytes =>
            Encoding.UTF8.GetBytes(ProtectedSeparatorString);

        public string ErrorMessage { get; private set; }

        public bool IsProtectedPackage(string packageFilename)
        {
            if (string.IsNullOrWhiteSpace(packageFilename) ||
                !File.Exists(packageFilename))
                return false;

            return FindMarkerOffset(
                packageFilename,
                ProtectedSeparatorBytes) > 0;
        }

        public ProtectedPackageManifest ReadManifest(string packageFilename)
        {
            try
            {
                var layout = ReadLayout(packageFilename);
                return layout.Manifest;
            }
            catch (Exception ex)
            {
                SetError(ex);
                return null;
            }
        }

        public bool PackageFile(
            string packageFilename,
            string exeFilename,
            string dataFilename,
            OfflineLicenseRequirement requirement)
        {
            ErrorMessage = null;

            try
            {
                if (!File.Exists(exeFilename))
                    throw new FileNotFoundException(
                        "Viewer executable does not exist.", exeFilename);

                if (!File.Exists(dataFilename))
                    throw new FileNotFoundException(
                        "Package data file does not exist.", dataFilename);

                if (requirement == null ||
                    string.IsNullOrWhiteSpace(requirement.CourseId))
                {
                    throw new InvalidOperationException(
                        "Protected packages require course licensing metadata.");
                }

                if (string.IsNullOrWhiteSpace(LicenseKeys.PublicKeyXml))
                {
                    throw new InvalidOperationException(
                        "Offline licensing has no public verification key.");
                }

                var manifest =
                    ProtectedPackageManifest.FromRequirement(requirement);

                var manifestBytes = SerializeManifest(manifest);

                byte[] encryptionKey;
                byte[] authenticationKey;

                DerivePayloadKeys(
                    manifestBytes,
                    out encryptionKey,
                    out authenticationKey);

                var iv = RandomBytes(IvLength);

                if (File.Exists(packageFilename))
                    File.Delete(packageFilename);

                long protectedStart;
                long cipherLengthPosition;
                long cipherStart;
                long cipherEnd;

                using (var outFs = new FileStream(
                    packageFilename,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None))
                {
                    using (var exeFs = new FileStream(
                        exeFilename,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read))
                    {
                        exeFs.CopyTo(outFs);
                    }

                    outFs.Write(
                        ProtectedSeparatorBytes,
                        0,
                        ProtectedSeparatorBytes.Length);

                    protectedStart = outFs.Position;

                    WriteInt32(outFs, manifestBytes.Length);
                    outFs.Write(manifestBytes, 0, manifestBytes.Length);
                    outFs.Write(iv, 0, iv.Length);

                    cipherLengthPosition = outFs.Position;
                    WriteInt64(outFs, 0L);

                    cipherStart = outFs.Position;

                    using (var aes = CreateAes(encryptionKey, iv))
                    using (var encryptor = aes.CreateEncryptor())
                    using (var crypto = new CryptoStream(
                        outFs,
                        encryptor,
                        CryptoStreamMode.Write,
                        true))
                    using (var input = new FileStream(
                        dataFilename,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read))
                    {
                        input.CopyTo(crypto);
                        crypto.FlushFinalBlock();
                    }

                    cipherEnd = outFs.Position;

                    var cipherLength = cipherEnd - cipherStart;

                    outFs.Position = cipherLengthPosition;
                    WriteInt64(outFs, cipherLength);

                    outFs.Position = cipherEnd;
                    outFs.Flush();
                }

                var authenticatedLength = cipherEnd - protectedStart;

                var mac = ComputeHmacForRange(
                    packageFilename,
                    protectedStart,
                    authenticatedLength,
                    authenticationKey);

                using (var append = new FileStream(
                    packageFilename,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.None))
                {
                    append.Write(mac, 0, mac.Length);
                    append.Flush();
                }

                Clear(encryptionKey);
                Clear(authenticationKey);

                return true;
            }
            catch (Exception ex)
            {
                SetError(ex);

                try
                {
                    if (File.Exists(packageFilename))
                        File.Delete(packageFilename);
                }
                catch
                {
                }

                return false;
            }
        }

        public bool UnpackageFile(
            string packageFilename,
            string outputPath,
            bool unZip = true)
        {
            ErrorMessage = null;

            try
            {
                if (!File.Exists(packageFilename))
                    throw new FileNotFoundException(
                        "Protected package does not exist.", packageFilename);

                if (Directory.Exists(outputPath))
                    throw new InvalidOperationException(
                        "Output path already exists.");

                var layout = ReadLayout(packageFilename);

                byte[] encryptionKey;
                byte[] authenticationKey;

                DerivePayloadKeys(
                    layout.ManifestBytes,
                    out encryptionKey,
                    out authenticationKey);

                var actualMac = ComputeHmacForRange(
                    packageFilename,
                    layout.ProtectedStart,
                    layout.AuthenticatedLength,
                    authenticationKey);

                if (!ConstantTimeEquals(actualMac, layout.ExpectedMac))
                {
                    throw new CryptographicException(
                        "Protected package integrity validation failed.");
                }

                Directory.CreateDirectory(outputPath);

                var exeFile = Path.Combine(
                    outputPath,
                    "WebPackageViewer.exe");

                using (var source = new FileStream(
                    packageFilename,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                using (var destination = new FileStream(
                    exeFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    CopyToBytes(
                        source,
                        destination,
                        layout.ExecutableLength);
                }

                var packageZip = Path.Combine(
                    outputPath,
                    "Packaged.zip");

                using (var source = new FileStream(
                    packageFilename,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    source.Seek(
                        layout.CipherStart,
                        SeekOrigin.Begin);

                    using (var limited = new LimitedReadStream(
                        source,
                        layout.CipherLength))
                    using (var aes = CreateAes(
                        encryptionKey,
                        layout.Iv))
                    using (var decryptor = aes.CreateDecryptor())
                    using (var crypto = new CryptoStream(
                        limited,
                        decryptor,
                        CryptoStreamMode.Read))
                    using (var destination = new FileStream(
                        packageZip,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        crypto.CopyTo(destination);
                    }
                }

                Clear(encryptionKey);
                Clear(authenticationKey);

                if (unZip)
                {
                    var legacyPackager = new FilePackager();

                    if (!legacyPackager.UnZipPackageInplace(
                        packageZip,
                        outputPath))
                    {
                        throw new InvalidOperationException(
                            "Unable to extract protected Web package. " +
                            legacyPackager.ErrorMessage);
                    }

                    try
                    {
                        File.Delete(packageZip);
                    }
                    catch
                    {
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                SetError(ex);

                try
                {
                    if (Directory.Exists(outputPath))
                        Directory.Delete(outputPath, true);
                }
                catch
                {
                }

                return false;
            }
        }

        private PackageLayout ReadLayout(string packageFilename)
        {
            var markerOffset = FindMarkerOffset(
                packageFilename,
                ProtectedSeparatorBytes);

            if (markerOffset < 0)
            {
                throw new InvalidOperationException(
                    "File is not a protected WebPackageViewer package.");
            }

            using (var stream = new FileStream(
                packageFilename,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (var reader = new BinaryReader(
                stream,
                Encoding.UTF8,
                true))
            {
                stream.Seek(markerOffset, SeekOrigin.Begin);

                var protectedStart = markerOffset;
                var manifestLength = reader.ReadInt32();

                if (manifestLength <= 0 ||
                    manifestLength > MaximumManifestLength)
                {
                    throw new InvalidOperationException(
                        "Protected package manifest is invalid.");
                }

                var manifestBytes = reader.ReadBytes(manifestLength);

                if (manifestBytes.Length != manifestLength)
                {
                    throw new EndOfStreamException(
                        "Protected package manifest is incomplete.");
                }

                var manifest = DeserializeManifest(manifestBytes);
                var iv = reader.ReadBytes(IvLength);

                if (iv.Length != IvLength)
                {
                    throw new EndOfStreamException(
                        "Protected package initialization vector is incomplete.");
                }

                var cipherLength = reader.ReadInt64();

                if (cipherLength <= 0)
                {
                    throw new InvalidOperationException(
                        "Protected package payload length is invalid.");
                }

                var cipherStart = stream.Position;
                var macPosition = checked(cipherStart + cipherLength);

                if (macPosition + MacLength > stream.Length)
                {
                    throw new EndOfStreamException(
                        "Protected package payload is incomplete.");
                }

                stream.Seek(macPosition, SeekOrigin.Begin);
                var expectedMac = reader.ReadBytes(MacLength);

                if (expectedMac.Length != MacLength)
                {
                    throw new EndOfStreamException(
                        "Protected package authentication code is incomplete.");
                }

                return new PackageLayout
                {
                    Manifest = manifest,
                    ManifestBytes = manifestBytes,
                    Iv = iv,
                    ProtectedStart = protectedStart,
                    CipherStart = cipherStart,
                    CipherLength = cipherLength,
                    AuthenticatedLength =
                        macPosition - protectedStart,
                    ExpectedMac = expectedMac,
                    ExecutableLength =
                        markerOffset - ProtectedSeparatorBytes.Length
                };
            }
        }

        private static byte[] SerializeManifest(
            ProtectedPackageManifest manifest)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(ProtectedPackageManifest));

                serializer.WriteObject(stream, manifest);
                return stream.ToArray();
            }
        }

        private static ProtectedPackageManifest DeserializeManifest(
            byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(ProtectedPackageManifest));

                var manifest = (ProtectedPackageManifest)
                    serializer.ReadObject(stream);

                if (manifest == null ||
                    string.IsNullOrWhiteSpace(manifest.CourseId))
                {
                    throw new InvalidOperationException(
                        "Protected package has no valid Course ID.");
                }

                return manifest;
            }
        }

        private static void DerivePayloadKeys(
            byte[] manifestBytes,
            out byte[] encryptionKey,
            out byte[] authenticationKey)
        {
            var publicKey = LicenseKeys.PublicKeyXml ?? string.Empty;
            var label = "WebPackageViewer|ProtectedPayload|V1|";
            var publicBytes = Encoding.UTF8.GetBytes(publicKey + label);

            var input = new byte[
                publicBytes.Length + manifestBytes.Length];

            Buffer.BlockCopy(
                publicBytes,
                0,
                input,
                0,
                publicBytes.Length);

            Buffer.BlockCopy(
                manifestBytes,
                0,
                input,
                publicBytes.Length,
                manifestBytes.Length);

            byte[] material;

            using (var sha = SHA512.Create())
                material = sha.ComputeHash(input);

            encryptionKey = new byte[32];
            authenticationKey = new byte[32];

            Buffer.BlockCopy(material, 0, encryptionKey, 0, 32);
            Buffer.BlockCopy(material, 32, authenticationKey, 0, 32);

            Clear(material);
            Clear(input);
            Clear(publicBytes);
        }

        private static Aes CreateAes(byte[] key, byte[] iv)
        {
            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;
            return aes;
        }

        private static byte[] RandomBytes(int length)
        {
            var bytes = new byte[length];

            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            return bytes;
        }

        private static byte[] ComputeHmacForRange(
            string filename,
            long offset,
            long length,
            byte[] key)
        {
            using (var hmac = new HMACSHA256(key))
            using (var stream = new FileStream(
                filename,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                stream.Seek(offset, SeekOrigin.Begin);

                var buffer = new byte[81920];
                long remaining = length;

                while (remaining > 0)
                {
                    var requested = (int)Math.Min(
                        buffer.Length,
                        remaining);

                    var read = stream.Read(
                        buffer,
                        0,
                        requested);

                    if (read <= 0)
                    {
                        throw new EndOfStreamException(
                            "Protected package ended while validating integrity.");
                    }

                    hmac.TransformBlock(
                        buffer,
                        0,
                        read,
                        null,
                        0);

                    remaining -= read;
                }

                hmac.TransformFinalBlock(
                    new byte[0],
                    0,
                    0);

                return hmac.Hash;
            }
        }

        private static long FindMarkerOffset(
            string filename,
            byte[] marker)
        {
            const int bufferSize = 4096;
            var buffer = new byte[bufferSize + marker.Length - 1];

            using (var fs = new FileStream(
                filename,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                long position = 0;
                int bytesRead;

                while ((bytesRead = fs.Read(
                    buffer,
                    0,
                    buffer.Length)) > 0)
                {
                    for (var i = 0;
                        i <= bytesRead - marker.Length;
                        i++)
                    {
                        var match = true;

                        for (var j = 0;
                            j < marker.Length;
                            j++)
                        {
                            if (buffer[i + j] != marker[j])
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                            return position + i + marker.Length;
                    }

                    if (bytesRead < marker.Length)
                        break;

                    position += bytesRead - marker.Length + 1;
                    fs.Seek(position, SeekOrigin.Begin);
                }
            }

            return -1;
        }

        private static void CopyToBytes(
            Stream source,
            Stream destination,
            long bytesToCopy,
            int bufferSize = 81920)
        {
            var buffer = new byte[bufferSize];
            long copied = 0;

            while (copied < bytesToCopy)
            {
                var requested = (int)Math.Min(
                    buffer.Length,
                    bytesToCopy - copied);

                var read = source.Read(
                    buffer,
                    0,
                    requested);

                if (read <= 0)
                {
                    throw new EndOfStreamException(
                        "Package executable prefix is incomplete.");
                }

                destination.Write(buffer, 0, read);
                copied += read;
            }
        }

        private static void WriteInt32(Stream stream, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteInt64(Stream stream, long value)
        {
            var bytes = BitConverter.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static bool ConstantTimeEquals(
            byte[] left,
            byte[] right)
        {
            if (left == null ||
                right == null ||
                left.Length != right.Length)
                return false;

            var difference = 0;

            for (var i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];

            return difference == 0;
        }

        private static void Clear(byte[] bytes)
        {
            if (bytes != null)
                Array.Clear(bytes, 0, bytes.Length);
        }

        private void SetError(Exception ex)
        {
            ErrorMessage = ex?.GetBaseException().Message;
        }

        private sealed class PackageLayout
        {
            public ProtectedPackageManifest Manifest { get; set; }
            public byte[] ManifestBytes { get; set; }
            public byte[] Iv { get; set; }
            public long ProtectedStart { get; set; }
            public long CipherStart { get; set; }
            public long CipherLength { get; set; }
            public long AuthenticatedLength { get; set; }
            public byte[] ExpectedMac { get; set; }
            public long ExecutableLength { get; set; }
        }

        private sealed class LimitedReadStream : Stream
        {
            private readonly Stream _inner;
            private long _remaining;

            public LimitedReadStream(Stream inner, long length)
            {
                _inner = inner;
                _remaining = length;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _remaining;

            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }

            public override int Read(
                byte[] buffer,
                int offset,
                int count)
            {
                if (_remaining <= 0)
                    return 0;

                count = (int)Math.Min(count, _remaining);
                var read = _inner.Read(buffer, offset, count);
                _remaining -= read;
                return read;
            }

            public override void Flush() { }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(
                byte[] buffer,
                int offset,
                int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
