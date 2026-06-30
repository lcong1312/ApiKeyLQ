using System;
using System.Web;
using System.Web.Mvc;
using ApiKey.Helpers;

namespace ApiKey.Filters
{
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var cookie = filterContext.HttpContext.Request.Cookies["admin_token"];
            if (cookie == null || string.IsNullOrEmpty(cookie.Value))
            {
                filterContext.Result = new RedirectResult("/quantri");
                return;
            }

            string email, role;
            if (!JwtHelper.ValidateToken(cookie.Value, out email, out role) || role != "Admin")
            {
                // Clear invalid token cookie
                var expiredCookie = new HttpCookie("admin_token")
                {
                    Expires = DateTime.Now.AddDays(-1),
                    Path = "/"
                };
                filterContext.HttpContext.Response.Cookies.Add(expiredCookie);

                filterContext.Result = new RedirectResult("/quantri");
                return;
            }

            // Put admin email in ViewBag and HttpContext.Items
            filterContext.Controller.ViewBag.AdminEmail = email;
            filterContext.HttpContext.Items["AdminEmail"] = email;

            base.OnActionExecuting(filterContext);
        }
    }
}
