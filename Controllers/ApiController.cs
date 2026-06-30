using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Mvc;
using ApiKey.Filters;
using ApiKey.Helpers;
using ApiKey.Models;
using Newtonsoft.Json;

namespace ApiKey.Controllers
{
    public class ApiController : Controller
    {
        private static readonly string[] DefaultFeatures =
        {
            "Category_Menu Chinh",
            "1_Toggle_Hack Map V2",
            "2_SeekBar_Cam Xa_0_20",
            "3_Toggle_Unlock FPS 120",
            "4_Button_Auto Ban Do",
            "5_Button_Unlock Skin",
            "6_Button_Aim Chieu Doan Huong",
            "7_Button_ESP",
            "8_Button_Nut + Thong Bao Ha",
            "9_Toggle_Time Hoi Chieu"
        };

        private static readonly string[] Activate1Features =
{
    "Collapse_Menu Chính_True",
    "CollapseAdd_Category_CHƠI KÍN AN TOÀN !!!",
    "CollapseAdd_Spinner_Chống Tố Cáo_Tắt,Tốt,Mạnh",
    "CollapseAdd_RichTextView_<font color='yellow'>HƯỚNG DẪN :</font> <b><i>Chống tố nhẹ khi nổ trụ đợi 5-10 giây nó tự đá ra sảnh không cần bật ra sảnh nhanh, còn chống tố mạnh sau khi nổ trụ đợi 5-10 giây tự đá ra khỏi game !</i></b>",
    "CollapseAdd_Toggle_Hack Map",
    "CollapseAdd_Toggle_Camera Cao_True",
    "CollapseAdd_Toggle_Hiện Lịch Sử",

    "Collapse_Unlock Skin",
    "CollapseAdd_Toggle_Unlock Skin",
    "CollapseAdd_Toggle_Thông Báo Skin",

    "Collapse_Macro Auto",
    "CollapseAdd_Toggle_Auto Bán Đồ",
    "CollapseAdd_Toggle_Auto Trừng Trị",
    "CollapseAdd_Toggle_Auto Băng Sương",
    "CollapseAdd_Toggle_Auto Bộc Phá",

    "Collapse_Đi Chiêu",
    "CollapseAdd_Toggle_Aim Elsu",
    "CollapseAdd_Toggle_ESP Elsu",
    "CollapseAdd_Toggle_Auto Yue",

    "Collapse_Đánh Máy",
    "CollapseAdd_Toggle_Fake Profile",
    "CollapseAdd_Toggle_Fake Rank",
    "CollapseAdd_Toggle_Fake Cục Đỏ",
    "CollapseAdd_Toggle_Auto Win",
    "CollapseAdd_Toggle_Auto Lose",
    "CollapseAdd_Toggle_Chạy Nhanh",
    "CollapseAdd_Toggle_Full Dame",
    "CollapseAdd_Toggle_Full Mana",
    "CollapseAdd_Toggle_Full Tiền",
    "CollapseAdd_Toggle_Full Vàng Trận",
    "CollapseAdd_Toggle_Hủy Hồi Chiêu",
    "CollapseAdd_Toggle_Miễn Nhiễm",
    "CollapseAdd_Toggle_Tự Động Chơi",

    "Collapse_Nhạc"
};

        // 1. POST /api/auth/check
        [HttpPost]
        public ActionResult CheckAuth()
        {
            return CheckAuthWithFeatures(DefaultFeatures);
        }

        [HttpPost]
        public ActionResult CheckAuth1()
        {
            return CheckAuthWithFeatures(Activate1Features);
        }

        [HttpPost]
        public ActionResult Activate()
        {
            return CheckAuthWithFeatures(DefaultFeatures);
        }

        [HttpPost]
        public ActionResult Activate1()
        {
            return CheckAuthWithFeatures(Activate1Features);
        }

        private ActionResult CheckAuthWithFeatures(string[] features)
        {
            try
            {
                var request = ReadEncryptedActivationRequest();
                if (request == null || string.IsNullOrEmpty(request.data))
                {
                    return EncryptedJson(new { success = false, message = "Invalid request", mes = "Invalid request" });
                }

                var payload = JsonConvert.DeserializeObject<ActivationPayload>(AesCryptoHelper.DecryptFromBase64(request.data));
                if (payload == null)
                {
                    return EncryptedJson(new { success = false, message = "Invalid request", mes = "Invalid request" });
                }

                string apiKey = payload.key;
                string deviceId = payload.device_id;
                string deviceName = payload.device_name;

                if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 5)
                {
                    return EncryptedJson(new { success = false, message = "Key phải có ít nhất 5 ký tự", mes = "Key phải có ít nhất 5 ký tự" });
                }

                var db = JsonDbHelper.Read();
                var key = db.ApiKeys.Find(k => k.KeyString == apiKey);
                if (key == null)
                {
                    return EncryptedJson(new { success = false, message = "Invalid API Key", mes = "Invalid API Key" });
                }

                if (key.Status == "Expired" || key.ExpiredAt < JsonDbHelper.VnNow)
                {
                    if (key.Status != "Expired")
                    {
                        key.Status = "Expired";
                        JsonDbHelper.Write(db);
                    }

                    return EncryptedJson(new { success = false, message = "Invalid API Key", mes = "Invalid API Key" });
                }

                if (key.Status == "Disabled")
                {
                    return EncryptedJson(new { success = false, message = "Invalid API Key", mes = "Invalid API Key" });
                }

                if (!key.AllowMultipleDevices && string.IsNullOrEmpty(key.DeviceId))
                {
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        key.DeviceId = deviceId;
                    }
                }
                else if (!key.AllowMultipleDevices && (string.IsNullOrEmpty(deviceId) || deviceId != key.DeviceId))
                {
                    return EncryptedJson(new { success = false, message = "Invalid Device ID", mes = "Invalid Device ID" });
                }

                string clientIp = GetClientIp();
                if (!string.IsNullOrEmpty(key.WhitelistIp) && !IsIpAllowed(key.WhitelistIp, clientIp))
                {
                    return EncryptedJson(new { success = false, message = "Invalid API Key", mes = "Invalid API Key" });
                }

                int todayCount = db.RequestLogs.FindAll(l => l.KeyId == key.Id && l.RequestTime.Date == JsonDbHelper.VnToday).Count;
                if (todayCount >= key.DailyLimit)
                {
                    return EncryptedJson(new { success = false, message = "Invalid API Key", mes = "Invalid API Key" });
                }

                key.TotalRequests++;

                int newLogId = db.RequestLogs.Count > 0 ? db.RequestLogs[db.RequestLogs.Count - 1].Id + 1 : 1;
                db.RequestLogs.Add(new JsonDbHelper.RequestLogModel
                {
                    Id = newLogId,
                    KeyId = key.Id,
                    ClientIp = clientIp,
                    RequestTime = JsonDbHelper.VnNow
                });

                JsonDbHelper.Write(db);

                int remainingDays = (key.ExpiredAt - JsonDbHelper.VnNow).Days;
                if (remainingDays < 0) remainingDays = 0;

                return EncryptedJson(new
                {
                    success = true,
                    ketqua = true,
                    thanhcong = true,
                    expires_at = key.ExpiredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    menu_setting = features,
                    menu_logo = "",
                    menu_name = key.Name,
                    menu_subtitle = key.Description ?? "",
                    message = "Login thành công",
                    mes = "Login thành công",
                    hansudung = remainingDays,
                    icon = "",
                    bidanh = key.Owner,
                    device_id = deviceId,
                    device_name = deviceName
                });
            }
            catch
            {
                return EncryptedJson(new { success = false, message = "Invalid request", mes = "Invalid request" });
            }
        }

        // 2. POST /api/keys/create
        [HttpPost]
        [AdminAuthorize]
        public ActionResult CreateKey(string name, string owner, string description, int? dailyLimit, string whitelistIp, string notes, string expiryMode, int? expiryDays, DateTime? expiryDate, bool allowMultipleDevices = false)
        {
            try
            {
                string apiKey = GenerateSecureApiKey();

                if (string.IsNullOrEmpty(name))
                {
                    name = "Key_" + apiKey.Substring(0, 8);
                }
                if (string.IsNullOrEmpty(owner))
                {
                    owner = "Default Owner";
                }

                DateTime expiredAt = JsonDbHelper.VnNow;
                if (expiryMode == "days")
                {
                    if (!expiryDays.HasValue || expiryDays.Value <= 0)
                    {
                        return Json(new { success = false, message = "Valid number of days is required." });
                    }
                    expiredAt = JsonDbHelper.VnNow.AddDays(expiryDays.Value);
                }
                else if (expiryMode == "date")
                {
                    if (!expiryDate.HasValue || expiryDate.Value <= JsonDbHelper.VnNow)
                    {
                        return Json(new { success = false, message = "Valid future expiry date is required." });
                    }
                    expiredAt = expiryDate.Value;
                }
                else
                {
                    return Json(new { success = false, message = "Invalid expiry mode." });
                }

                var db = JsonDbHelper.Read();

                int newId = db.ApiKeys.Count > 0 ? db.ApiKeys.Max(k => k.Id) + 1 : 1;
                var newKey = new ApiKeyModel
                {
                    Id = newId,
                    Name = name,
                    KeyString = apiKey,
                    Owner = owner,
                    Description = description,
                    Status = "Active",
                    CreatedAt = JsonDbHelper.VnNow,
                    ExpiredAt = expiredAt,
                    DailyLimit = (!dailyLimit.HasValue || dailyLimit.Value <= 0) ? 10000 : dailyLimit.Value,
                    TotalRequests = 0,
                    WhitelistIp = whitelistIp,
                    Notes = notes,
                    DeviceId = null,
                    AllowMultipleDevices = allowMultipleDevices
                };

                db.ApiKeys.Add(newKey);
                JsonDbHelper.Write(db);

                return Json(new { success = true, message = "API Key created successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // 3. POST /api/keys/update
        [HttpPost]
        [AdminAuthorize]
        public ActionResult UpdateKey(int id, string name, string owner, string description, int? dailyLimit, string whitelistIp, string notes, bool allowMultipleDevices = false)
        {
            try
            {
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(owner))
                {
                    return Json(new { success = false, message = "Name and Owner are required." });
                }

                var db = JsonDbHelper.Read();
                var key = db.ApiKeys.Find(k => k.Id == id);
                if (key == null)
                {
                    return Json(new { success = false, message = "API Key not found." });
                }

                key.Name = name;
                key.Owner = owner;
                key.Description = description;
                key.DailyLimit = (!dailyLimit.HasValue || dailyLimit.Value <= 0) ? 10000 : dailyLimit.Value;
                key.WhitelistIp = whitelistIp;
                key.Notes = notes;
                key.AllowMultipleDevices = allowMultipleDevices;
                if (allowMultipleDevices)
                {
                    key.DeviceId = null;
                }

                JsonDbHelper.Write(db);

                return Json(new { success = true, message = "API Key updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // 4. POST /api/keys/renew
        [HttpPost]
        [AdminAuthorize]
        public ActionResult RenewKey(int id, string expiryMode, int? expiryDays, DateTime? expiryDate)
        {
            try
            {
                var db = JsonDbHelper.Read();
                var key = db.ApiKeys.Find(k => k.Id == id);
                if (key == null)
                {
                    return Json(new { success = false, message = "API Key not found." });
                }

                DateTime newExpiredAt;
                if (expiryMode == "days")
                {
                    if (!expiryDays.HasValue || expiryDays.Value <= 0)
                    {
                        return Json(new { success = false, message = "Valid number of days is required." });
                    }
                    DateTime start = (key.ExpiredAt > JsonDbHelper.VnNow) ? key.ExpiredAt : JsonDbHelper.VnNow;
                    newExpiredAt = start.AddDays(expiryDays.Value);
                }
                else if (expiryMode == "date")
                {
                    if (!expiryDate.HasValue || expiryDate.Value <= JsonDbHelper.VnNow)
                    {
                        return Json(new { success = false, message = "Valid future expiry date is required." });
                    }
                    newExpiredAt = expiryDate.Value;
                }
                else
                {
                    return Json(new { success = false, message = "Invalid expiry mode." });
                }

                key.ExpiredAt = newExpiredAt;
                key.Status = "Active"; // Reset to Active since it is extended

                JsonDbHelper.Write(db);

                return Json(new { success = true, message = "API Key renewed successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // 5. POST /api/keys/toggle-status
        [HttpPost]
        [AdminAuthorize]
        public ActionResult ToggleStatus(int id, string status)
        {
            try
            {
                if (status != "Active" && status != "Disabled")
                {
                    return Json(new { success = false, message = "Invalid status target. Can only toggle Active/Disabled." });
                }

                var db = JsonDbHelper.Read();
                var key = db.ApiKeys.Find(k => k.Id == id);
                if (key == null)
                {
                    return Json(new { success = false, message = "API Key not found." });
                }

                key.Status = status;
                JsonDbHelper.Write(db);

                return Json(new { success = true, message = "API Key status updated to " + status + "." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // 6. POST /api/keys/reset-device
        [HttpPost]
        [AdminAuthorize]
        public ActionResult ResetDevice(int id)
        {
            try
            {
                var db = JsonDbHelper.Read();
                var key = db.ApiKeys.Find(k => k.Id == id);
                if (key == null)
                {
                    return Json(new { success = false, message = "API Key not found." });
                }

                key.DeviceId = null;
                JsonDbHelper.Write(db);

                return Json(new { success = true, message = "Reset thiết bị thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // 7. POST /api/keys/delete
        [HttpPost]
        [AdminAuthorize]
        public ActionResult DeleteKey(int id)
        {
            try
            {
                var db = JsonDbHelper.Read();
                var key = db.ApiKeys.Find(k => k.Id == id);
                if (key == null)
                {
                    return Json(new { success = false, message = "API Key not found." });
                }

                db.ApiKeys.Remove(key);
                db.RequestLogs.RemoveAll(l => l.KeyId == id); // Cascade delete logs

                JsonDbHelper.Write(db);

                return Json(new { success = true, message = "API Key deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        private string GenerateSecureApiKey()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var result = new char[8];
            using (var rng = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[8];
                rng.GetBytes(bytes);
                for (int i = 0; i < 8; i++)
                {
                    result[i] = chars[bytes[i] % chars.Length];
                }
            }
            return "vietcong_" + new string(result);
        }

        private ActivationRequest ReadEncryptedActivationRequest()
        {
            Request.InputStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(Request.InputStream))
            {
                string body = reader.ReadToEnd();
                Request.InputStream.Seek(0, SeekOrigin.Begin);
                return JsonConvert.DeserializeObject<ActivationRequest>(body);
            }
        }

        private ActionResult EncryptedJson(object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            return Json(new { data = AesCryptoHelper.EncryptToBase64(json) });
        }

        private bool IsIpAllowed(string whitelistIp, string clientIp)
        {
            var allowedIps = whitelistIp.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ip => ip.Trim())
                .ToList();

            foreach (var ip in allowedIps)
            {
                if (ip == clientIp || ip == "127.0.0.1" && clientIp == "::1" || ip == "::1" && clientIp == "127.0.0.1")
                {
                    return true;
                }
            }

            return false;
        }

        private string GetClientIp()
        {
            string ip = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (string.IsNullOrEmpty(ip))
            {
                ip = Request.ServerVariables["REMOTE_ADDR"];
            }
            if (string.IsNullOrEmpty(ip))
            {
                ip = Request.UserHostAddress;
            }
            if (ip == "::1")
            {
                return "127.0.0.1";
            }
            return ip;
        }

        private class ActivationRequest
        {
            public string data { get; set; }
        }

        private class ActivationPayload
        {
            public string key { get; set; }
            public string device_id { get; set; }
            public string device_name { get; set; }
        }
    }
}
