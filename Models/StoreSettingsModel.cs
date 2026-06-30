using System;
using System.Collections.Generic;

namespace ApiKey.Models
{
    public class StorePackage
    {
        public int Id { get; set; }
        public int Days { get; set; }
        public int Price { get; set; }
    }

    public class StoreProduct
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = "KeyGuard License Key";
        public string ProductDescription { get; set; } = "Kich hoat day du tinh nang cua cong bao mat API KeyGuard.";
        public string LogoIcon { get; set; } = "fa-solid fa-key";
        public string DownloadUrl { get; set; } = "";
        public List<StorePackage> Packages { get; set; } = new List<StorePackage>();
    }

    public class StoreSettingsModel
    {
        public string ProductName { get; set; } = "KeyGuard License Key";
        public string ProductDescription { get; set; } = "Kích hoạt đầy đủ tính năng của cổng bảo mật API KeyGuard. Giới hạn 5,000 requests/ngày, quản trị vòng đời và giám sát lưu lượng thời gian thực.";
        public string LogoIcon { get; set; } = "fa-solid fa-key";
        public string DownloadUrl { get; set; } = "";
        public List<StorePackage> Packages { get; set; } = new List<StorePackage>();
        public List<StoreProduct> Products { get; set; } = new List<StoreProduct>();
    }
}
