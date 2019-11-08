using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace FFWebApp462
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Default",
                url: "api/{controller}/{action}/{id}",
                defaults: new { controller = "Values", action = "Get", id = UrlParameter.Optional }
            );
        }
    }
}
