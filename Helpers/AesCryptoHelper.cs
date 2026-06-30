using System;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ApiKey.Helpers
{
    public static class AesCryptoHelper
    {
        private const string DefaultKey = "12345678901234567890123456789012";
        private const string DefaultIv = "1234567890123456";

        public static string EncryptToBase64(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
            byte[] encrypted = Transform(plainBytes, encrypt: true);
            return Convert.ToBase64String(encrypted);
        }

        public static string DecryptFromBase64(string cipherText)
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText ?? string.Empty);
            byte[] decrypted = Transform(cipherBytes, encrypt: false);
            return Encoding.UTF8.GetString(decrypted);
        }

        private static byte[] Transform(byte[] input, bool encrypt)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = NormalizeBytes(ConfigurationManager.AppSettings["AesKey"], DefaultKey, 32);
                aes.IV = NormalizeBytes(ConfigurationManager.AppSettings["AesIv"], DefaultIv, 16);
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
                using (transform)
                using (var output = new MemoryStream())
                using (var crypto = new CryptoStream(output, transform, CryptoStreamMode.Write))
                {
                    crypto.Write(input, 0, input.Length);
                    crypto.FlushFinalBlock();
                    return output.ToArray();
                }
            }
        }

        private static byte[] NormalizeBytes(string value, string fallback, int size)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(value) ? fallback : value);
            if (bytes.Length == size)
            {
                return bytes;
            }

            var normalized = new byte[size];
            Array.Copy(bytes, normalized, Math.Min(bytes.Length, size));
            return normalized;
        }
    }
}
