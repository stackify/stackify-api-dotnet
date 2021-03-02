using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace FullFrameworkWebApp.Controllers
{
    public class HomeController : Controller
    {
        public HomeController()
        {
            StackifyLib.Config.ReadStackifyJSONConfig();
            StackifyLib.Utils.StackifyAPILogger.OnLogMessage += StackifyAPILogger_OnLogMessage;
            StackifyLib.Utils.StackifyAPILogger.LogEnabled = true;
        }
        private static void StackifyAPILogger_OnLogMessage(string data)
        {
            Debug.WriteLine(data);
        }
        public ActionResult Index()
        {
            StackifyLib.Logger.Queue("DEBUG", "#Christian My log message");
            StackifyLib.Logger.QueueException("#Christian Test exception", new Exception("Hello World"));

            string baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string jsonPath = Path.Combine(baseDirectory, "Stackify.json");
            string IISBaseDirectory = HostingEnvironment.ApplicationPhysicalPath;

            Debug.WriteLine($"Path: {baseDirectory} - {jsonPath}");
            Debug.WriteLine($"IISPath: {IISBaseDirectory}");

            Debug.WriteLine($"AppName: {StackifyLib.Config.AppName}");
            Debug.WriteLine($"Environment: {StackifyLib.Config.Environment}");
            Debug.WriteLine($"ApiKey: {StackifyLib.Config.ApiKey}");

            ViewBag.JsonPath = jsonPath;
            return View();
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
    }
}