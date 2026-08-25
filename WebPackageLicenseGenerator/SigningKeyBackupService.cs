using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WebPackageLicenseGenerator
{
    public static class SigningKeyBackupService
    {
        private static readonly byte[] Magic =
            Encoding.ASCII.GetBytes("WPKB1");

        private const int Iterations = 200000;
        private const int SaltLength = 32;
        private const int IvLength = 16;
        private const int MacLength = 32;

        public static void ExportBackup(
            string outputFile,
            string password)
        {
            ValidatePassword(password);

            var privateKeyXml =
                SigningKeyStore.LoadPrivateKeyXml();

            var plaintext =
                Encoding.UTF8.GetBytes(privateKeyXml);

            var salt = RandomBytes(SaltLength);
            var iv = RandomBytes(IvLength);

            byte[] encryptionKey;
            byte[] authenticationKey;

            DeriveKeys(
                password,
                salt,
                Iterations,
                out encryptionKey,
                out authenticationKey);

            var ciphertext =
                Encrypt(
                    plaintext,
                    encryptionKey,
                    iv);

            var authenticatedData =
                BuildAuthenticatedData(
                    salt,
                    iv,
                    ciphertext,
                    Iterations);

            byte[] mac;

            using (var hmac =
                new HMACSHA256(authenticationKey))
            {
                mac = hmac.ComputeHash(authenticatedData);
            }

            using (var stream = File.Create(outputFile))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(Iterations);

                writer.Write(salt.Length);
                writer.Write(salt);

                writer.Write(iv.Length);
                writer.Write(iv);

                writer.Write(ciphertext.Length);
                writer.Write(ciphertext);

                writer.Write(mac.Length);
                writer.Write(mac);
            }

            Clear(encryptionKey);
            Clear(authenticationKey);
            Clear(plaintext);
        }

        public static void RestoreBackup(
            string inputFile,
            string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    "Enter the recovery-backup password.");
            }

            byte[] salt;
            byte[] iv;
            byte[] ciphertext;
            byte[] expectedMac;
            int iterations;

            using (var stream = File.OpenRead(inputFile))
            using (var reader = new BinaryReader(stream))
            {
                var magic = reader.ReadBytes(Magic.Length);

                if (!ArraysEqual(magic, Magic))
                {
                    throw new InvalidOperationException(
                        "This is not a Web Package signing-key backup.");
                }

                iterations = reader.ReadInt32();

                if (iterations < 10000 ||
                    iterations > 5000000)
                {
                    throw new InvalidOperationException(
                        "The signing-key backup has invalid key-derivation settings.");
                }

                salt =
                    ReadSizedBytes(
                        reader,
                        16,
                        128,
                        "salt");

                iv =
                    ReadSizedBytes(
                        reader,
                        IvLength,
                        IvLength,
                        "initialization vector");

                ciphertext =
                    ReadSizedBytes(
                        reader,
                        1,
                        1024 * 1024,
                        "encrypted key");

                expectedMac =
                    ReadSizedBytes(
                        reader,
                        MacLength,
                        MacLength,
                        "authentication code");

                if (stream.Position != stream.Length)
                {
                    throw new InvalidOperationException(
                        "The signing-key backup contains unexpected data.");
                }
            }

            byte[] encryptionKey;
            byte[] authenticationKey;

            DeriveKeys(
                password,
                salt,
                iterations,
                out encryptionKey,
                out authenticationKey);

            var authenticatedData =
                BuildAuthenticatedData(
                    salt,
                    iv,
                    ciphertext,
                    iterations);

            byte[] actualMac;

            using (var hmac =
                new HMACSHA256(authenticationKey))
            {
                actualMac =
                    hmac.ComputeHash(authenticatedData);
            }

            if (!ConstantTimeEquals(
                actualMac,
                expectedMac))
            {
                Clear(encryptionKey);
                Clear(authenticationKey);

                throw new InvalidOperationException(
                    "The backup password is incorrect, or the backup file has been modified.");
            }

            var plaintext =
                Decrypt(
                    ciphertext,
                    encryptionKey,
                    iv);

            try
            {
                var privateKeyXml =
                    Encoding.UTF8.GetString(plaintext);

                SigningKeyStore.InstallPrivateKeyXml(
                    privateKeyXml);
            }
            finally
            {
                Clear(encryptionKey);
                Clear(authenticationKey);
                Clear(plaintext);
            }
        }

        private static void ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password) ||
                password.Length < 12)
            {
                throw new InvalidOperationException(
                    "Use a recovery-backup password of at least 12 characters.");
            }
        }

        private static void DeriveKeys(
            string password,
            byte[] salt,
            int iterations,
            out byte[] encryptionKey,
            out byte[] authenticationKey)
        {
            using (var derive =
                new Rfc2898DeriveBytes(
                    password,
                    salt,
                    iterations))
            {
                var material = derive.GetBytes(64);

                encryptionKey =
                    material.Take(32).ToArray();

                authenticationKey =
                    material.Skip(32).Take(32).ToArray();

                Clear(material);
            }
        }

        private static byte[] Encrypt(
            byte[] plaintext,
            byte[] key,
            byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using (var output = new MemoryStream())
                using (var crypto =
                    new CryptoStream(
                        output,
                        aes.CreateEncryptor(),
                        CryptoStreamMode.Write))
                {
                    crypto.Write(
                        plaintext,
                        0,
                        plaintext.Length);

                    crypto.FlushFinalBlock();
                    return output.ToArray();
                }
            }
        }

        private static byte[] Decrypt(
            byte[] ciphertext,
            byte[] key,
            byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using (var input = new MemoryStream(ciphertext))
                using (var crypto =
                    new CryptoStream(
                        input,
                        aes.CreateDecryptor(),
                        CryptoStreamMode.Read))
                using (var output = new MemoryStream())
                {
                    crypto.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        private static byte[] BuildAuthenticatedData(
            byte[] salt,
            byte[] iv,
            byte[] ciphertext,
            int iterations)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(iterations);

                writer.Write(salt.Length);
                writer.Write(salt);

                writer.Write(iv.Length);
                writer.Write(iv);

                writer.Write(ciphertext.Length);
                writer.Write(ciphertext);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] RandomBytes(int length)
        {
            var bytes = new byte[length];

            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            return bytes;
        }

        private static byte[] ReadSizedBytes(
            BinaryReader reader,
            int minimumLength,
            int maximumLength,
            string label)
        {
            var length = reader.ReadInt32();

            if (length < minimumLength ||
                length > maximumLength)
            {
                throw new InvalidOperationException(
                    "The signing-key backup has an invalid " +
                    label +
                    ".");
            }

            var bytes = reader.ReadBytes(length);

            if (bytes.Length != length)
            {
                throw new InvalidOperationException(
                    "The signing-key backup is incomplete.");
            }

            return bytes;
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

        private static bool ArraysEqual(
            byte[] left,
            byte[] right)
        {
            if (left == null ||
                right == null ||
                left.Length != right.Length)
                return false;

            for (var i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return false;

            return true;
        }

        private static void Clear(byte[] bytes)
        {
            if (bytes != null)
                Array.Clear(bytes, 0, bytes.Length);
        }
    }
}
