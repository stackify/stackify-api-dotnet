using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using log4net.Config;

namespace FFWebApp462
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            var test2 = new FileInfo(@"C:\Dev\stackify-api-dotnet\Src\FFWebApp462\log4net.config");

            var test = XmlConfigurator.Configure(test2);

            StackifyLib.Utils.StackifyAPILogger.LogEnabled = true;
            StackifyLib.Utils.StackifyAPILogger.OnLogMessage += StackifyAPILogger_OnLogMessage;
        }

        static void StackifyAPILogger_OnLogMessage(string data)
        {
            Debug.WriteLine(data);
        }
    }
}
