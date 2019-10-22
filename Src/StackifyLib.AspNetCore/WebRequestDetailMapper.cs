using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using StackifyLib.Models;

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
            {
                return;
            }

            Load(context, detail);
        }

        private static void Load(HttpContext context, WebRequestDetail detail)
        {
            if (context == null || context.Request == null)
            {
                return;
            }

            var request = context.Request;

            try
            {
                detail.HttpMethod = request.Method;
                detail.UserIPAddress = context.Connection?.RemoteIpAddress?.ToString();
                detail.RequestProtocol = request.IsHttps ? "https" : "http";
                detail.RequestUrl = $"{detail.RequestProtocol}//{request.Host}{request.Path}";
                detail.MVCAction = context.GetRouteValue("action")?.ToString();
                detail.MVCController = context.GetRouteValue("controller")?.ToString();

                if (string.IsNullOrEmpty(detail.MVCAction) == false && string.IsNullOrEmpty(detail.MVCController) == false)
                {
                    detail.ReportingUrl = $"{detail.MVCController}.{detail.MVCAction}";
                }
            }
            catch (Exception)
            {
                // ignored
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

                    if (Config.ErrorHeaderBadKeys.Contains("cookie") == false)
                    {
                        Config.ErrorHeaderBadKeys.Add("cookie");
                    }

                    if (Config.ErrorHeaderBadKeys.Contains("authorization") == false)
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
            }
            catch (Exception)
            {
                // ignored
            }
        }

        internal static Dictionary<string, string> ToKeyValues(IEnumerable<KeyValuePair<string, StringValues>> collection, List<string> goodKeys, List<string> badKeys)
        {
            var items = new Dictionary<string, string>();

            foreach (KeyValuePair<string, StringValues> item in collection)
            {
                var key = item.Key;

                try
                {
                    object val = item.Value.ToString();

                    if (val != null && string.IsNullOrWhiteSpace(val.ToString()) == false && items.ContainsKey(key) == false)
                    {
                        AddKey(key, val.ToString(), items, goodKeys, badKeys);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return items;
        }

        internal static Dictionary<string, string> ToKeyValues(IEnumerable<KeyValuePair<string, string>> collection, List<string> goodKeys, List<string> badKeys)
        {
            var items = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> item in collection)
            {
                var key = item.Key;

                try
                {
                    object val = item.Value;

                    if (val != null && string.IsNullOrWhiteSpace(val.ToString()) == false && items.ContainsKey(key) == false)
                    {
                        AddKey(key, val.ToString(), items, goodKeys, badKeys);
                    }
                }
                catch (Exception)
                {
                    // ignored
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
            if (goodKeys != null && goodKeys.Any() && goodKeys.Any(x => x.Equals(key, StringComparison.CurrentCultureIgnoreCase)) == false)
            {
                return;
            }
            dictionary.Add(key,value);
        }
    }
}