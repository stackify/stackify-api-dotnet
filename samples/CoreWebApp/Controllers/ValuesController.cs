using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
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
