using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using FFWebApp462.Models;
using log4net;
using StackifyLib;

namespace FFWebApp462.Controllers
{
    public class ValuesController : ApiController
    {
        private static ILog _log4NetLogger = LogManager.GetLogger(typeof(ValuesController));

        // GET api/values
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        public IEnumerable<string> GetLog4Net()
        {
            _log4NetLogger.Debug("Log4Net Test Debug", new {clientId = 20});
            _log4NetLogger.Info("Log4Net Test Info", new { clientId = 20 });
            _log4NetLogger.Error("Log4Net Test Error", new { clientId = 20 });
            _log4NetLogger.Fatal("Log4Net Test Fatal", new { clientId = 20 });

            _log4NetLogger.Info(new {client = 99});

            return new string[] { "value3", "value4" };
        }

        // GET api/values/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        public void Post(ValueModel value)
        {
            var test = value.Value;

            StackifyLib.Logger.QueueException(new Exception("FF462 Failure"));
        }

        // PUT api/values/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        public void Delete(int id)
        {
        }
    }
}
