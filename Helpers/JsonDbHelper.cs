using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using ApiKey.Models;

namespace ApiKey.Helpers
{
    public static class JsonDbHelper
    {
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "db.json");
        private static readonly object LockObj = new object();

        public static DateTime VnNow => DateTime.UtcNow.AddHours(7);
        public static DateTime VnToday => DateTime.UtcNow.AddHours(7).Date;

        public class DbSchema
        {
            public List<AdminModel> Admins { get; set; } = new List<AdminModel>();
            public List<ApiKeyModel> ApiKeys { get; set; } = new List<ApiKeyModel>();
            public List<RequestLogModel> RequestLogs { get; set; } = new List<RequestLogModel>();
            
            private StoreSettingsModel _storeSettings;
            public StoreSettingsModel StoreSettings
            {
                get
                {
                    if (_storeSettings == null)
                    {
                        _storeSettings = new StoreSettingsModel();
                    }
                    return _storeSettings;
                }
                set { _storeSettings = value; }
            }
        }

        public class RequestLogModel
        {
            public int Id { get; set; }
            public int KeyId { get; set; }
            public DateTime RequestTime { get; set; }
            public string ClientIp { get; set; }
        }

        public static DbSchema Read()
        {
            lock (LockObj)
            {
                if (!File.Exists(DbPath))
                {
                    string dir = Path.GetDirectoryName(DbPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var schema = new DbSchema();
                    schema.Admins.Add(new AdminModel
                    {
                        Id = 1,
                        Email = "admin@gmail.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                        CreatedAt = VnNow
                    });

                    schema.StoreSettings.Packages = new List<StorePackage>
                    {
                        new StorePackage { Id = 1, Days = 7, Price = 50000 },
                        new StorePackage { Id = 2, Days = 30, Price = 150000 }
                    };
                    NormalizeStoreSettings(schema.StoreSettings);

                    File.WriteAllText(DbPath, JsonConvert.SerializeObject(schema, Formatting.Indented));
                    return schema;
                }

                try
                {
                    string json = File.ReadAllText(DbPath);
                    var schema = JsonConvert.DeserializeObject<DbSchema>(json) ?? new DbSchema();
                    if (schema.StoreSettings.Packages == null || schema.StoreSettings.Packages.Count == 0)
                    {
                        schema.StoreSettings.Packages = new List<StorePackage>
                        {
                            new StorePackage { Id = 1, Days = 7, Price = 50000 },
                            new StorePackage { Id = 2, Days = 30, Price = 150000 }
                        };
                    }
                    NormalizeStoreSettings(schema.StoreSettings);
                    return schema;
                }
                catch
                {
                    var schema = new DbSchema();
                    NormalizeStoreSettings(schema.StoreSettings);
                    return schema;
                }
            }
        }

        public static void Write(DbSchema schema)
        {
            lock (LockObj)
            {
                File.WriteAllText(DbPath, JsonConvert.SerializeObject(schema, Formatting.Indented));
            }
        }

        public static void NormalizeStoreSettings(StoreSettingsModel settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.Packages == null)
            {
                settings.Packages = new List<StorePackage>();
            }

            if (settings.Packages.Count == 0)
            {
                settings.Packages.Add(new StorePackage { Id = 1, Days = 7, Price = 50000 });
                settings.Packages.Add(new StorePackage { Id = 2, Days = 30, Price = 150000 });
            }

            if (settings.Products == null)
            {
                settings.Products = new List<StoreProduct>();
            }

            if (settings.Products.Count == 0)
            {
                settings.Products.Add(new StoreProduct
                {
                    Id = 1,
                    ProductName = settings.ProductName,
                    ProductDescription = settings.ProductDescription,
                    LogoIcon = settings.LogoIcon,
                    DownloadUrl = settings.DownloadUrl,
                    Packages = settings.Packages
                });
            }

            int productId = 1;
            foreach (var product in settings.Products)
            {
                product.Id = productId++;

                if (string.IsNullOrWhiteSpace(product.ProductName))
                {
                    product.ProductName = "San pham " + product.Id;
                }

                if (string.IsNullOrWhiteSpace(product.LogoIcon))
                {
                    product.LogoIcon = "fa-solid fa-key";
                }

                if (product.Packages == null)
                {
                    product.Packages = new List<StorePackage>();
                }

                if (product.Packages.Count == 0)
                {
                    product.Packages.Add(new StorePackage { Id = 1, Days = 7, Price = 50000 });
                }

                int packageId = 1;
                foreach (var package in product.Packages)
                {
                    package.Id = packageId++;
                }
            }

            var firstProduct = settings.Products[0];
            settings.ProductName = firstProduct.ProductName;
            settings.ProductDescription = firstProduct.ProductDescription;
            settings.LogoIcon = firstProduct.LogoIcon;
            settings.DownloadUrl = firstProduct.DownloadUrl;
            settings.Packages = firstProduct.Packages;
        }
    }
}
