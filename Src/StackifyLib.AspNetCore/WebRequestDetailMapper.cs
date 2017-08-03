using StackifyLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace StackifyLib.AspNetCore
{
    internal class WebRequestDetailMapper
    {
        /// <summary>
        /// Event handler for when we need to collect web request details
        /// </summary>
        /// <param name="detail"></param>
        internal static void WebRequestDetail_SetWebRequestDetail(WebRequestDetail detail)
        {
            //make sure we have access to the context
            var context = Configure.ServiceProvider?.GetService<IHttpContextAccessor>()?.HttpContext;

            if (context == null)
                return;

            Load(context, detail);
        }

        private static void Load(HttpContext context, WebRequestDetail detail)
        {
            if (context == null || context.Request == null)
                return;

            HttpRequest request = context.Request;

            try
            {
                detail.HttpMethod = request.Method;

                detail.UserIPAddress = context?.Connection?.RemoteIpAddress?.ToString();


                //if (context.Items != null && context.Items.Contains("Stackify.ReportingUrl"))
                //{
                //    ReportingUrl = context.Items["Stackify.ReportingUrl"].ToString();
                //}


                if (request.IsHttps)
                {
                    detail.RequestProtocol = "https";
                }
                else
                {
                    detail.RequestProtocol = "http";
                }
                detail.RequestUrl = detail.RequestProtocol + "//" + request.Host + request.Path;


                detail.MVCAction = context.GetRouteValue("action")?.ToString();
                detail.MVCController = context.GetRouteValue("controller")?.ToString();

                if (!string.IsNullOrEmpty(detail.MVCAction) && !string.IsNullOrEmpty(detail.MVCController))
                {
                    detail.ReportingUrl = detail.MVCController + "." + detail.MVCAction;
                }


                //if (request.AppRelativeCurrentExecutionFilePath != null)
                //{
                //    RequestUrlRoot = request.AppRelativeCurrentExecutionFilePath.TrimStart('~');
                //}

            }
            catch (Exception)
            {

            }



            try
            {

                if (request.QueryString != null)
                {
                    detail.QueryString = ToKeyValues(request.Query, null, null);
                }

                if (request.Headers != null && Config.CaptureErrorHeaders)
                {
                    if (Config.ErrorHeaderBadKeys == null)
                    {
                        Config.ErrorHeaderBadKeys = new List<string>();
                    }

                    if (!Config.ErrorHeaderBadKeys.Contains("cookie"))
                    {
                        Config.ErrorHeaderBadKeys.Add("cookie");
                    }

                    if (!Config.ErrorHeaderBadKeys.Contains("authorization"))
                    {
                        Config.ErrorHeaderBadKeys.Add("authorization");
                    }

                    detail.Headers = ToKeyValues(request.Headers, Config.ErrorHeaderGoodKeys, Config.ErrorHeaderBadKeys);
                }

                if (request.Cookies != null && Config.CaptureErrorCookies)
                {
                    detail.Cookies = ToKeyValues(request.Cookies, Config.ErrorCookiesGoodKeys, Config.ErrorCookiesBadKeys);
                }

                if (request.Form != null && Config.CaptureErrorPostdata)
                {
                    detail.PostData = ToKeyValues(request.Form, null, null);
                }

                //sessions return a byte array...
                //if (context.Session != null && Config.CaptureSessionVariables && Config.ErrorSessionGoodKeys.Any())
                //{
                //    SessionData = new Dictionary<string, string>();

                //    foreach (var key in Config.ErrorSessionGoodKeys)
                //    {
                //        SessionData[key] = context.Session
                //    }


                //}

                //if (Config.CaptureErrorPostdata)
                //{
                //    var contentType = context.Request.Headers["Content-Type"];

                //    if (contentType != "text/html" && contentType != "application/x-www-form-urlencoded" &&
                //        context.Request.RequestType != "GET")
                //    {
                //        int length = 4096;
                //        string postBody = new StreamReader(context.Request.InputStream).ReadToEnd();
                //        if (postBody.Length < length)
                //        {
                //            length = postBody.Length;
                //        }

                //        PostDataRaw = postBody.Substring(0, length);
                //    }
                //}
            }
            catch (Exception)
            {
            }
        }

        //IEnumerable<KeyValuePair<string, StringValues>>
        //IEnumerable<KeyValuePair<string, string>>
        internal static Dictionary<string, string> ToKeyValues(IEnumerable<KeyValuePair<string, StringValues>> collection, List<string> goodKeys, List<string> badKeys)
        {
            //var keys = collection.Keys;
            var items = new Dictionary<string, string>();

            foreach (var item in collection)
            {
                string key = item.Key;
                try
                {
                    object val = item.Value.ToString();

                    if (val != null && !string.IsNullOrWhiteSpace(val.ToString()) && items.ContainsKey(key))
                    {
                        AddKey(key, val.ToString(), items, goodKeys, badKeys);
                    }

                }
                catch (Exception)
                {

                }
            }

            return items;
        }

        internal static Dictionary<string, string> ToKeyValues(IEnumerable<KeyValuePair<string, string>> collection, List<string> goodKeys, List<string> badKeys)
        {
            //var keys = collection.Keys;
            var items = new Dictionary<string, string>();

            foreach (var item in collection)
            {
                string key = item.Key;
                try
                {
                    object val = item.Value;

                    if (val != null && !string.IsNullOrWhiteSpace(val.ToString()) && items.ContainsKey(key))
                    {
                        AddKey(key, val.ToString(), items, goodKeys, badKeys);
                    }

                }
                catch (Exception)
                {

                }
            }

            return items;
        }

        internal static void AddKey(string key, string value, Dictionary<string, string> dictionary, List<string> goodKeys, List<string> badKeys)
        {
            //is this key in the bad key list?
            if (badKeys != null && badKeys.Any(x => x.Equals(key, StringComparison.CurrentCultureIgnoreCase)))
            {
                dictionary[key] = "X-MASKED-X";
                return;
            }
            //if not in the good key list, return
            //if good key list is empty, we let it take it
            else if (goodKeys != null && goodKeys.Any() && !goodKeys.Any(x => x.Equals(key, StringComparison.CurrentCultureIgnoreCase)))
            {
                return;
            }

            dictionary[key] = value;
        }
    }
}
