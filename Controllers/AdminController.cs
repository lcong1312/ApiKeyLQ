using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ApiKey.Filters;
using ApiKey.Helpers;
using ApiKey.Models;
using Newtonsoft.Json;

namespace ApiKey.Controllers
{
    [AdminAuthorize]
    public class AdminController : Controller
    {
        // GET /Admin/Dashboard
        public ActionResult Dashboard()
        {
            var model = new DashboardViewModel();
            var db = JsonDbHelper.Read();

            // 1. Total Counters
            model.TotalKeys = db.ApiKeys.Count;
            model.ActiveKeys = db.ApiKeys.Count(k => k.Status == "Active" && k.ExpiredAt >= JsonDbHelper.VnNow);
            model.DisabledKeys = db.ApiKeys.Count(k => k.Status == "Disabled");
            model.ExpiredKeys = db.ApiKeys.Count(k => k.Status == "Expired" || (k.Status == "Active" && k.ExpiredAt < JsonDbHelper.VnNow));
            model.TotalRequests = db.RequestLogs.Count;
            model.TodayRequests = db.RequestLogs.Count(l => l.RequestTime.Date == JsonDbHelper.VnToday);

            // 2. Top API Keys
            model.TopKeys = db.ApiKeys
                .Select(k => new TopKeyMetric
                {
                    Name = k.Name,
                    Owner = k.Owner,
                    KeyString = k.KeyString,
                    RequestCount = db.RequestLogs.FindAll(l => l.KeyId == k.Id).Count
                })
                .OrderByDescending(t => t.RequestCount)
                .Take(5)
                .ToList();

            // 3. Weekly Request stats (last 7 days)
            var dateCounts = new Dictionary<DateTime, int>();
            for (int i = 6; i >= 0; i--)
            {
                dateCounts[JsonDbHelper.VnToday.AddDays(-i)] = 0;
            }

            var recentLogs = db.RequestLogs.FindAll(l => l.RequestTime.Date >= JsonDbHelper.VnToday.AddDays(-6));
            foreach (var log in recentLogs)
            {
                DateTime date = log.RequestTime.Date;
                if (dateCounts.ContainsKey(date))
                {
                    dateCounts[date]++;
                }
            }

            foreach (var kvp in dateCounts)
            {
                model.Last7DaysRequests.Add(new DailyRequestMetric
                {
                    DateString = kvp.Key.ToString("dd/MM"),
                    RequestCount = kvp.Value
                });
            }

            return View(model);
        }

        // GET /Admin/Keys
        public ActionResult Keys(string search = "", string status = "All", string expired = "All", string sort = "CreatedAt", string direction = "DESC", int page = 1)
        {
            int pageSize = 10;
            var db = JsonDbHelper.Read();

            // 1. Filter
            var query = db.ApiKeys.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(k => k.Name.ToLower().Contains(searchLower) 
                                      || k.Owner.ToLower().Contains(searchLower) 
                                      || k.KeyString.ToLower().Contains(searchLower) 
                                      || (k.Description ?? "").ToLower().Contains(searchLower));
            }

            if (status != "All")
            {
                if (status == "Expired")
                {
                    query = query.Where(k => k.Status == "Expired" || (k.Status == "Active" && k.ExpiredAt < JsonDbHelper.VnNow));
                }
                else if (status == "Active")
                {
                    query = query.Where(k => k.Status == "Active" && k.ExpiredAt >= JsonDbHelper.VnNow);
                }
                else
                {
                    query = query.Where(k => k.Status == status);
                }
            }

            if (expired != "All")
            {
                if (expired == "Yes")
                {
                    query = query.Where(k => k.ExpiredAt < JsonDbHelper.VnNow);
                }
                else if (expired == "No")
                {
                    query = query.Where(k => k.ExpiredAt >= JsonDbHelper.VnNow);
                }
            }

            // 2. Sort
            bool isAsc = direction.ToUpper() == "ASC";
            switch (sort.ToLower())
            {
                case "name":
                    query = isAsc ? query.OrderBy(k => k.Name) : query.OrderByDescending(k => k.Name);
                    break;
                case "owner":
                    query = isAsc ? query.OrderBy(k => k.Owner) : query.OrderByDescending(k => k.Owner);
                    break;
                case "status":
                    query = isAsc ? query.OrderBy(k => k.Status) : query.OrderByDescending(k => k.Status);
                    break;
                case "expiredat":
                    query = isAsc ? query.OrderBy(k => k.ExpiredAt) : query.OrderByDescending(k => k.ExpiredAt);
                    break;
                case "dailylimit":
                    query = isAsc ? query.OrderBy(k => k.DailyLimit) : query.OrderByDescending(k => k.DailyLimit);
                    break;
                case "totalrequests":
                    query = isAsc ? query.OrderBy(k => k.TotalRequests) : query.OrderByDescending(k => k.TotalRequests);
                    break;
                default:
                    query = isAsc ? query.OrderBy(k => k.CreatedAt) : query.OrderByDescending(k => k.CreatedAt);
                    break;
            }

            // 3. Paginate
            int totalRecords = query.Count();
            int offset = (page - 1) * pageSize;
            if (offset < 0) offset = 0;

            var list = query.Skip(offset).Take(pageSize).ToList();

            // Update expired status dynamically in memory list for representation
            foreach (var key in list)
            {
                if (key.Status == "Active" && key.ExpiredAt < JsonDbHelper.VnNow)
                {
                    key.Status = "Expired";
                }
            }

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Expired = expired;
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            return View(list);
        }

        // GET /Admin/StoreSettings
        public ActionResult StoreSettings()
        {
            var db = JsonDbHelper.Read();
            return View(db.StoreSettings);
        }

        // POST /Admin/UpdateStoreSettings
        [HttpPost]
        public ActionResult UpdateStoreSettings(StoreSettingsModel settings, string packagesJson, string productsJson)
        {
            try
            {
                if (settings == null)
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                var db = JsonDbHelper.Read();

                if (!string.IsNullOrWhiteSpace(productsJson))
                {
                    var products = JsonConvert.DeserializeObject<List<StoreProduct>>(productsJson) ?? new List<StoreProduct>();
                    if (products.Count == 0)
                    {
                        return Json(new { success = false, message = "Vui lòng thêm ít nhất một sản phẩm." });
                    }

                    db.StoreSettings.Products = products;
                }
                else
                {
                    var packages = JsonConvert.DeserializeObject<List<StorePackage>>(packagesJson);
                    db.StoreSettings.ProductName = settings.ProductName;
                    db.StoreSettings.ProductDescription = settings.ProductDescription;
                    db.StoreSettings.LogoIcon = settings.LogoIcon;
                    db.StoreSettings.DownloadUrl = settings.DownloadUrl;
                    db.StoreSettings.Packages = packages ?? new List<StorePackage>();
                    db.StoreSettings.Products = new List<StoreProduct>
                    {
                        new StoreProduct
                        {
                            Id = 1,
                            ProductName = settings.ProductName,
                            ProductDescription = settings.ProductDescription,
                            LogoIcon = settings.LogoIcon,
                            DownloadUrl = settings.DownloadUrl,
                            Packages = db.StoreSettings.Packages
                        }
                    };
                }

                JsonDbHelper.NormalizeStoreSettings(db.StoreSettings);
                JsonDbHelper.Write(db);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu cấu hình: " + ex.Message });
            }
        }
    }
}
