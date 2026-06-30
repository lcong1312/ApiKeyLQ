using System;

namespace ApiKey.Models
{
    public class ApiKeyModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string KeyString { get; set; }
        public string Owner { get; set; }
        public string Description { get; set; }
        public string Status { get; set; } // "Active", "Disabled", "Expired"
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public int DailyLimit { get; set; }
        public int TotalRequests { get; set; }
        public string WhitelistIp { get; set; }
        public string Notes { get; set; }
        public string DeviceId { get; set; }
        public bool AllowMultipleDevices { get; set; }

        // Helper to check if physically expired
        public bool IsExpired()
        {
            return ExpiredAt < ApiKey.Helpers.JsonDbHelper.VnNow;
        }
    }
}
