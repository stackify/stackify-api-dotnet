using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreWebApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;

namespace CoreWebApp.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        static NLog.Logger nlog = LogManager.GetCurrentClassLogger();

        private static ILogger<ValuesController> _Logger = ApplicationLogging.CreateLogger<ValuesController>();

        [HttpGet]
        public IEnumerable<string> Get()
        {
            //  nlog.Debug("Callng Get() method");
            var ex1 = new Exception("Failure");
            //Will create a StackifyError
            _Logger.LogError(ex1, "Test error");

            //Will not create a StackifyError
            _Logger.LogError("From helper method");

            try
            {
                throw new InvalidOperationException("Bring the boom");
            }
            catch (Exception ex)
            {
                nlog.Error(ex, "Uh oh");
            }

            SomeOtherClass soc = new SomeOtherClass();
            soc.DoBadWebRequest();

            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        //raw
        [HttpPost]
        public void Post()
        {
            var ex1 = new Exception("Failure");
            //Will create a StackifyError
            _Logger.LogError(ex1, "Test POST error");
        }

        [HttpPost]
        [Route("PostValue")]
        public void PostValue([FromBody]ValueModel thing)
        {
            var test = thing.Value;

            var ex1 = new Exception("Failure");
            //Will create a StackifyError
            _Logger.LogError(ex1, "Test VALUE POST error");
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
