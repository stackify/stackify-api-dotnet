using System;
using System.IO;
using System.Text;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Web.LayoutRenderers;
using StackifyLib.Models;
using Newtonsoft.Json;

namespace NLog.Targets.Stackify
{
    [LayoutRenderer("stackify-http")]
    public class StackifyHttpLayoutRenderer : AspNetLayoutRendererBase
    {
        protected override void DoAppend(StringBuilder builder, LogEventInfo logEvent)
        {
            var httpRequest = HttpContextAccessor.HttpContext;
            if (httpRequest != null)
            {
#if NETFULL
                WebRequestDetail webRequest = new WebRequestDetail();
                webRequest.Load(httpRequest);

                string result = JsonConvert.SerializeObject(webRequest);
                builder.Append(result);
#endif
            }
        }
    }
}
