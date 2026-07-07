using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ApiKey.Helpers;
using ApiKey.Models;

namespace ApiKey.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var db = JsonDbHelper.Read();
            return View(db.StoreSettings);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        [HttpPost]
        public async Task<ActionResult> CreatePayment(string email, int days, int productId = 1)
        {
            if (string.IsNullOrEmpty(email))
            {
                return Json(new { success = false, message = "Email không hợp lệ." });
            }

            try
            {
                // Resolve package from db.json settings dynamically
                var db = JsonDbHelper.Read();
                var product = db.StoreSettings.Products.FirstOrDefault(p => p.Id == productId)
                    ?? db.StoreSettings.Products.FirstOrDefault();
                if (product == null)
                {
                    return Json(new { success = false, message = "Chưa có sản phẩm nào được cấu hình." });
                }

                var package = product.Packages.FirstOrDefault(p => p.Days == days);
                if (package == null)
                {
                    return Json(new { success = false, message = $"Gói bán {days} ngày không được hỗ trợ cho sản phẩm này." });
                }

                int price = package.Price;

                // Generate a unique order code (numeric for PayOS)
                Random rand = new Random();
                long orderCode = long.Parse(DateTime.Now.ToString("yyMMddHHmmss") + rand.Next(10, 99));

                string description = $"KEYGUARD {days}D {orderCode % 10000}";
                if (description.Length > 25)
                {
                    description = description.Substring(0, 25);
                }

                // Save order as pending in orders.json
                var order = new PayosOrder
                {
                    OrderCode = orderCode,
                    Email = email,
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    Days = days,
                    Amount = price,
                    Status = "Pending",
                    GeneratedApiKey = null,
                    CreatedAt = JsonDbHelper.VnNow
                };
                OrderDbHelper.Add(order);

                string host = Request.Url.GetLeftPart(UriPartial.Authority);
                string cancelUrl = $"{host}/?payment=cancelled&orderCode={orderCode}";
                string returnUrl = $"{host}/?payment=success&orderCode={orderCode}";

                // Call PayOS API to generate payment link
                var paymentResult = await PayosHelper.CreatePaymentLink(
                    orderCode,
                    price,
                    description,
                    cancelUrl,
                    returnUrl,
                    $"{product.ProductName} {days} Ngày"
                );

                return Json(new { success = true, checkoutUrl = paymentResult.CheckoutUrl, orderCode = orderCode });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tạo giao dịch thanh toán: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<ActionResult> CheckPaymentStatus(long orderCode)
        {
            var order = OrderDbHelper.Get(orderCode);
            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng." }, JsonRequestBehavior.AllowGet);
            }

            // If already processed as PAID, return the existing key directly
            if (order.Status == "PAID")
            {
                return Json(new { success = true, status = "PAID", apiKey = order.GeneratedApiKey }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                // Retrieve payment status from PayOS API
                var paymentDetails = await PayosHelper.GetPaymentLinkInformation(orderCode);

                if (paymentDetails.Status == "PAID")
                {
                    // Generate new API Key
                    string keyString = "vietcong_" + Guid.NewGuid().ToString("n").Substring(0, 12);

                    // Insert key into db.json
                    var db = JsonDbHelper.Read();
                    var productName = string.IsNullOrWhiteSpace(order.ProductName)
                        ? db.StoreSettings.ProductName
                        : order.ProductName;
                    var newKey = new ApiKeyModel
                    {
                        Id = db.ApiKeys.Count > 0 ? db.ApiKeys.Max(k => k.Id) + 1 : 1,
                        Name = $"{productName} - {order.Days} Ngày - {order.Email}",
                        KeyString = keyString,
                        Owner = order.Email,
                        Description = $"Thanh toán PayOS cho {productName}, gói {order.Days} ngày",
                        Status = "Active",
                        CreatedAt = JsonDbHelper.VnNow,
                        ExpiredAt = JsonDbHelper.VnNow.AddDays(order.Days),
                        DailyLimit = 5000,
                        TotalRequests = 0,
                        WhitelistIp = null,
                        Notes = $"Mã đơn hàng: {order.OrderCode}",
                        DeviceId = null,
                        AllowMultipleDevices = false
                    };

                    db.ApiKeys.Add(newKey);
                    JsonDbHelper.Write(db);
                    EmailHelper.TrySendLicenseKey(order.Email, keyString, productName, order.Days, newKey.ExpiredAt);

                    // Update order status in orders.json
                    OrderDbHelper.Update(orderCode, "PAID", keyString);

                    return Json(new { success = true, status = "PAID", apiKey = keyString }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = true, status = paymentDetails.Status }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi kiểm tra thanh toán: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
