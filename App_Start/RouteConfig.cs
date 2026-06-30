using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace ApiKey
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "ApiKeysActivate",
                url: "api/keys/activate",
                defaults: new { controller = "Api", action = "Activate" }
            );

            routes.MapRoute(
                name: "ApiKeysActivate1",
                url: "api/keys/activate1",
                defaults: new { controller = "Api", action = "Activate1" }
            );

            routes.MapRoute(
                name: "QuanTri",
                url: "quantri",
                defaults: new { controller = "Auth", action = "Login" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
