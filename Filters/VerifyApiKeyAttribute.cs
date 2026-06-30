using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace ApiKey.Filters
{
    public class VerifyApiKeyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            var response = filterContext.HttpContext.Response;

            // 1. Extract API Key and Device ID from Headers, Query, Form, or JSON Body
            string apiKey = request.Headers["x-api-key"]
                         ?? request.Headers["apikey"]
                         ?? request.QueryString["apikey"]
                         ?? request.Form["apikey"];

            string deviceId = request.Headers["x-device-id"]
                           ?? request.Headers["deviceid"]
                           ?? request.Headers["thietbiid"]
                           ?? request.QueryString["deviceid"]
                           ?? request.QueryString["thietbiid"]
                           ?? request.Form["deviceid"]
                           ?? request.Form["thietbiid"];

            if ((string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deviceId)) && request.ContentType != null && request.ContentType.Contains("application/json"))
            {
                try
                {
                    request.InputStream.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(request.InputStream))
                    {
                        string body = reader.ReadToEnd();
                        request.InputStream.Seek(0, SeekOrigin.Begin); // reset for binder
                        
                        var json = JsonConvert.DeserializeAnonymousType(body, new { apikey = "", deviceid = "", deviceId = "", thietbiid = "" });
                        if (json != null)
                        {
                            if (string.IsNullOrEmpty(apiKey)) apiKey = json.apikey;
                            if (string.IsNullOrEmpty(deviceId))
                            {
                                deviceId = !string.IsNullOrEmpty(json.deviceId) ? json.deviceId : (!string.IsNullOrEmpty(json.deviceid) ? json.deviceid : json.thietbiid);
                            }
                        }
                    }
                }
                catch { /* ignored */ }
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                ReturnUnauthorized(filterContext, "Invalid API Key");
                return;
            }

            // 2. Validate API Key from JSON DB
            var db = ApiKey.Helpers.JsonDbHelper.Read();
            var key = db.ApiKeys.Find(k => k.KeyString == apiKey);

            if (key == null)
            {
                ReturnUnauthorized(filterContext, "Invalid API Key");
                return;
            }

            // Check expired status or physical expiration date
            if (key.Status == "Expired" || key.ExpiredAt < ApiKey.Helpers.JsonDbHelper.VnNow)
            {
                if (key.Status != "Expired")
                {
                    key.Status = "Expired";
                    ApiKey.Helpers.JsonDbHelper.Write(db);
                }
                ReturnUnauthorized(filterContext, "Invalid API Key");
                return;
            }

            // Check if key is disabled
            if (key.Status == "Disabled")
            {
                ReturnUnauthorized(filterContext, "Invalid API Key");
                return;
            }

            // Check Device ID
            if (!key.AllowMultipleDevices && string.IsNullOrEmpty(key.DeviceId))
            {
                if (!string.IsNullOrEmpty(deviceId))
                {
                    key.DeviceId = deviceId;
                    ApiKey.Helpers.JsonDbHelper.Write(db);
                }
            }
            else if (!key.AllowMultipleDevices)
            {
                if (string.IsNullOrEmpty(deviceId) || deviceId != key.DeviceId)
                {
                    ReturnUnauthorized(filterContext, "Invalid Device ID");
                    return;
                }
            }

            // Check IP Whitelist if configured
            string clientIp = GetClientIp(request);
            if (!string.IsNullOrEmpty(key.WhitelistIp))
            {
                var allowedIps = key.WhitelistIp.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(ip => ip.Trim())
                                             .ToList();
                
                bool ipMatched = false;
                foreach (var ip in allowedIps)
                {
                    if (ip == clientIp || ip == "127.0.0.1" && clientIp == "::1" || ip == "::1" && clientIp == "127.0.0.1")
                    {
                        ipMatched = true;
                        break;
                    }
                }

                if (!ipMatched)
                {
                    ReturnUnauthorized(filterContext, "Invalid API Key");
                    return;
                }
            }

            // Check Daily Limit
            int todayCount = db.RequestLogs.FindAll(l => l.KeyId == key.Id && l.RequestTime.Date == ApiKey.Helpers.JsonDbHelper.VnToday).Count;
            if (todayCount >= key.DailyLimit)
            {
                ReturnUnauthorized(filterContext, "Invalid API Key");
                return;
            }

            // 3. Increment requests count & log request
            key.TotalRequests++;

            int newLogId = db.RequestLogs.Count > 0 ? db.RequestLogs[db.RequestLogs.Count - 1].Id + 1 : 1;
            db.RequestLogs.Add(new ApiKey.Helpers.JsonDbHelper.RequestLogModel
            {
                Id = newLogId,
                KeyId = key.Id,
                ClientIp = clientIp,
                RequestTime = ApiKey.Helpers.JsonDbHelper.VnNow
            });

            ApiKey.Helpers.JsonDbHelper.Write(db);

            // Set items in HttpContext for controller actions
            filterContext.HttpContext.Items["ApiKey"] = apiKey;
            filterContext.HttpContext.Items["Owner"] = key.Owner;
            filterContext.HttpContext.Items["KeyId"] = key.Id;

            base.OnActionExecuting(filterContext);
        }

        private void ReturnUnauthorized(ActionExecutingContext filterContext, string message)
        {
            filterContext.Result = new JsonResult
            {
                Data = new { success = false, message = message },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
            filterContext.HttpContext.Response.StatusCode = 401; // Unauthorized
        }

        private string GetClientIp(HttpRequestBase request)
        {
            string ip = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (string.IsNullOrEmpty(ip))
            {
                ip = request.ServerVariables["REMOTE_ADDR"];
            }
            if (string.IsNullOrEmpty(ip))
            {
                ip = request.UserHostAddress;
            }
            if (ip == "::1")
            {
                return "127.0.0.1";
            }
            return ip;
        }
    }
}
