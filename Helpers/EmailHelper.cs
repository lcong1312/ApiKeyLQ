using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace ApiKey.Helpers
{
    public static class EmailHelper
    {
        private static string GetSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        private static bool GetBoolSetting(string key, bool defaultValue)
        {
            bool value;
            return bool.TryParse(GetSetting(key), out value) ? value : defaultValue;
        }

        private static int GetIntSetting(string key, int defaultValue)
        {
            int value;
            return int.TryParse(GetSetting(key), out value) ? value : defaultValue;
        }

        public static bool IsConfigured()
        {
            if (!GetBoolSetting("SmtpEnabled", false))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(GetSetting("SmtpHost"))
                && !string.IsNullOrWhiteSpace(GetSetting("SmtpUsername"))
                && !string.IsNullOrWhiteSpace(GetSetting("SmtpPassword"))
                && !string.IsNullOrWhiteSpace(GetSetting("SmtpFromEmail"));
        }

        public static bool TrySendLicenseKey(string toEmail, string apiKey, string productName, int days, DateTime expiredAt)
        {
            if (string.IsNullOrWhiteSpace(toEmail) || string.IsNullOrWhiteSpace(apiKey) || !IsConfigured())
            {
                return false;
            }

            try
            {
                SendLicenseKey(toEmail, apiKey, productName, days, expiredAt);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SendLicenseKey(string toEmail, string apiKey, string productName, int days, DateTime expiredAt)
        {
            string fromEmail = GetSetting("SmtpFromEmail");
            string fromName = GetSetting("SmtpFromName");
            string host = GetSetting("SmtpHost");
            int port = GetIntSetting("SmtpPort", 587);
            bool enableSsl = GetBoolSetting("SmtpEnableSsl", true);

            using (var message = new MailMessage())
            {
                message.From = string.IsNullOrWhiteSpace(fromName)
                    ? new MailAddress(fromEmail)
                    : new MailAddress(fromEmail, fromName, Encoding.UTF8);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = "License key của bạn";
                message.SubjectEncoding = Encoding.UTF8;
                message.BodyEncoding = Encoding.UTF8;
                message.IsBodyHtml = true;
                message.Body = BuildLicenseEmailBody(apiKey, productName, days, expiredAt);

                using (var client = new SmtpClient(host, port))
                {
                    client.EnableSsl = enableSsl;
                    client.Credentials = new NetworkCredential(GetSetting("SmtpUsername"), GetSetting("SmtpPassword"));
                    client.Send(message);
                }
            }
        }

        private static string BuildLicenseEmailBody(string apiKey, string productName, int days, DateTime expiredAt)
        {
            string safeProductName = WebUtility.HtmlEncode(productName ?? "San pham");
            string safeApiKey = WebUtility.HtmlEncode(apiKey);

            return $@"
<!doctype html>
<html>
<body style=""margin:0;padding:24px;background:#f6f7fb;font-family:Arial,sans-serif;color:#111827;"">
  <div style=""max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;padding:24px;"">
    <h2 style=""margin:0 0 12px;color:#10b981;"">Thanh toán thành công</h2>
    <p style=""margin:0 0 16px;line-height:1.5;"">Cảm ơn bạn đã mua hàng cho <strong>{safeProductName}</strong>.</p>
    <div style=""margin:18px 0;padding:16px;background:#0f172a;border-radius:10px;color:#34d399;font-family:Consolas,monospace;font-size:18px;word-break:break-all;"">
      {safeApiKey}
    </div>
    <p style=""margin:0 0 8px;line-height:1.5;""><strong>Thời hạn:</strong> {days} ngày</p>
    <p style=""margin:0 0 18px;line-height:1.5;""><strong>Hết hạn:</strong> {expiredAt:dd/MM/yyyy HH:mm}</p>
  </div>
</body>
</html>";
        }
    }
}
