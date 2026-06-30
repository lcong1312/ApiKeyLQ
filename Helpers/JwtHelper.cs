using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace ApiKey.Helpers
{
    public static class JwtHelper
    {
        private static readonly string Secret = ConfigurationManager.AppSettings["JwtSecret"] ?? "APIKeyManagementSystemSecretKeyMustBe32BytesLong!";

        public static string CreateToken(string email, string role = "Admin", int expireMinutes = 1440)
        {
            var header = new { alg = "HS256", typ = "JWT" };
            var payload = new
            {
                sub = email,
                role = role,
                exp = DateTimeOffset.UtcNow.AddMinutes(expireMinutes).ToUnixTimeSeconds()
            };

            string headerJson = JsonConvert.SerializeObject(header);
            string payloadJson = JsonConvert.SerializeObject(payload);

            string headerEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            string payloadEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

            string input = headerEncoded + "." + payloadEncoded;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret)))
            {
                byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
                string signatureEncoded = Base64UrlEncode(signatureBytes);
                return input + "." + signatureEncoded;
            }
        }

        public static bool ValidateToken(string token, out string email, out string role)
        {
            email = null;
            role = null;
            if (string.IsNullOrEmpty(token)) return false;

            try
            {
                string[] parts = token.Split('.');
                if (parts.Length != 3) return false;

                string headerEncoded = parts[0];
                string payloadEncoded = parts[1];
                string signatureEncoded = parts[2];

                string input = headerEncoded + "." + payloadEncoded;
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret)))
                {
                    byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
                    string calculatedSignature = Base64UrlEncode(signatureBytes);
                    if (signatureEncoded != calculatedSignature) return false;
                }

                string payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(payloadEncoded));
                var payload = JsonConvert.DeserializeAnonymousType(payloadJson, new { sub = "", role = "", exp = 0L });

                if (payload.exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;

                email = payload.sub;
                role = payload.role;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        private static byte[] Base64UrlDecode(string input)
        {
            string output = input.Replace("-", "+").Replace("_", "/");
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
            }
            return Convert.FromBase64String(output);
        }
    }
}
