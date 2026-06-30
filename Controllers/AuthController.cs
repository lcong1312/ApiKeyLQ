using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.Mvc;
using ApiKey.Helpers;

namespace ApiKey.Controllers
{
    public class AuthController : Controller
    {
        // GET /Auth/Login
        [HttpGet]
        public ActionResult Login()
        {
            // If already logged in, redirect to admin dashboard
            var cookie = Request.Cookies["admin_token"];
            if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
            {
                string email, role;
                if (JwtHelper.ValidateToken(cookie.Value, out email, out role) && role == "Admin")
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
            }
            return View();
        }

        // POST /Auth/Login (AJAX)
        [HttpPost]
        public ActionResult Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                return Json(new { success = false, message = "Email and Password are required." });
            }

            try
            {
                var db = JsonDbHelper.Read();
                var admin = db.Admins.Find(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

                if (admin == null || !PasswordHasher.VerifyPassword(password, admin.PasswordHash))
                {
                    return Json(new { success = false, message = "Incorrect Email or Password." });
                }

                // Credentials are valid, generate JWT token
                string token = JwtHelper.CreateToken(email, "Admin");

                // Save token in HTTP-only Cookie
                var cookie = new HttpCookie("admin_token", token)
                {
                    HttpOnly = true,
                    Expires = DateTime.Now.AddHours(24),
                    Path = "/"
                };
                Response.Cookies.Add(cookie);

                return Json(new { success = true, redirectUrl = Url.Action("Dashboard", "Admin") });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // GET /Auth/Logout
        [HttpGet]
        public ActionResult Logout()
        {
            var cookie = new HttpCookie("admin_token")
            {
                Expires = DateTime.Now.AddDays(-1),
                Path = "/"
            };
            Response.Cookies.Add(cookie);
            return Redirect("/quantri");
        }
    }
}
