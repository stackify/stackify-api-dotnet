using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CoreWebApp.Controllers
{
    public class SomeOtherClass
    {
        public SomeOtherClass()
        {
        }

        public async Task<string> DoBadWebRequest()
        {
            try
            {
                using (HttpClient hc = new HttpClient())
                {
                    var data = await hc.GetStringAsync("https://stackify.comm");
                    return data;
                }
            }
            catch (Exception ex)
            {
                var logger = ApplicationLogging.CreateLogger<SomeOtherClass>();
                logger.LogError("Error! " + ex.ToString());
            }

            return null;
        }
    }
}
