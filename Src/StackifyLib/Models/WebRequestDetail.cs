using System.Runtime.Serialization;
using StackifyLib.Web;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Web;
using System.Web.Routing;

namespace StackifyLib.Models
{
    [DataContract]
    public class WebRequestDetail
    {
        private StackifyError _Error;
        public WebRequestDetail(StackifyError error)
        {
            _Error = error;
            if (System.Web.HttpContext.Current != null)
            {
                Load(System.Web.HttpContext.Current);
            }
        }

        [DataMember]
        public string UserIPAddress { get; set; }

        [DataMember]
        public string HttpMethod { get; set; }

        [DataMember]
        public string RequestProtocol { get; set; }

        [DataMember]
        public string RequestUrl { get; set; }
        [DataMember]
        public string RequestUrlRoot { get; set; }

        [DataMember]
        public string ReportingUrl { get; set; }


        [DataMember]
        public string ReferralUrl { get; set; }

        [DataMember]
        public Dictionary<string, string> Headers { get; set; }

        [DataMember]
        public Dictionary<string, string> Cookies { get; set; }

        [DataMember]
        public Dictionary<string, string> QueryString { get; set; }

        [DataMember]
        public Dictionary<string, string> PostData { get; set; }

        [DataMember]
        public Dictionary<string, string> SessionData { get; set; }

        [DataMember]
        public string PostDataRaw { get; set; }

        [DataMember]
        public string MVCAction { get; set; }

        [DataMember]
        public string MVCController { get; set; }

        [DataMember]
        public string MVCArea { get; set; }

        private void Load(HttpContext context)
        {
            if (context == null || context.Request == null)
                return;

            HttpRequest request = context.Request;
            
            try
            {
                HttpMethod = request.RequestType;
                UserIPAddress = request.UserHostAddress;

                if (context.Items != null && context.Items.Contains("Stackify.ReportingUrl"))
                {
                    ReportingUrl = context.Items["Stackify.ReportingUrl"].ToString();
                }

                //We be nice to detect if it is a web sockets connection and denote that in the protocol field
                //do we care about things like HTTP/1.1 or the port?
                if (request.IsSecureConnection)
                {
                    RequestProtocol = "https";
                }
                else
                {
                    RequestProtocol = "http";
                }

                if (request.Url != null)
                {
                    RequestUrl = request.Url.ToString();
                }

                if (request.AppRelativeCurrentExecutionFilePath != null)
                {
                    RequestUrlRoot = request.AppRelativeCurrentExecutionFilePath.TrimStart('~');
                }

                RouteResolver resolver = new RouteResolver(context);

                var route = resolver.GetRoute();

                MVCArea = route.Area;
                MVCController = route.Controller;
                MVCAction = route.Action;

                if (string.IsNullOrEmpty(ReportingUrl) && route != null && !string.IsNullOrEmpty(route.Action))
                {
                    ReportingUrl = route.ToString();
                }
            }
            catch (Exception)
            {

            }

           

            try
            {
                if (request.QueryString != null)
                {
                    QueryString = ToKeyValues(request.QueryString, false);
                }

                if (request.ServerVariables != null)
                {
                    List<string> badKeys = new List<string>();
                    badKeys.AddRange(new string[] { "all_http", "all_raw", "http_cookie" });

                    var serverVars = ToKeyValues(request.ServerVariables, false, badKeys);
                    foreach (var serverVar in serverVars)
                    {
                        _Error.ServerVariables[serverVar.Key] = serverVar.Value;
                    }
                }

                if (request.Headers != null)
                {
                    List<string> badKeys = new List<string>();
                    badKeys.AddRange(new string[] { "cookie" });

                    Headers = ToKeyValues(request.Headers, false, badKeys);
                }

                if (request.Cookies != null)
                {
                    Cookies = ToKeyValues(request.Cookies, true);

                    //if (Cookies != null)
                    //{
                    //    foreach (var item in Cookies)
                    //    {
                    //        Cookies[item.Key] = "X-MASKED-X";
                    //    }
                    //}

                    //if (Cookies.ContainsKey(".ASPXAUTH"))
                    //{
                    //    Cookies[".ASPXAUTH"] = "X-MASKED-X";
                    //}
                }

                if (request.Form != null)
                {
                    PostData = ToKeyValues(request.Form, true);

                    //if (PostData != null)
                    //{
                    //    foreach (var item in PostData)
                    //    {
                    //        PostData[item.Key] = "X-MASKED-X";
                    //    }
                    //}
                }

                if (context.Session != null)
                {
                    SessionData = ToKeyValues(context.Session, true);
                }

                var contentType = context.Request.Headers["Content-Type"];

                if (contentType != "text/html" && contentType != "application/x-www-form-urlencoded" && context.Request.RequestType != "GET")
                {
                    int length = 4096;
                    string postBody = new StreamReader(context.Request.InputStream).ReadToEnd();
                    if (postBody.Length < length)
                    {
                        length = postBody.Length;
                    }

                    PostDataRaw = postBody.Substring(0, length);
                }
            }
            catch (Exception)
            {
            }
        }


        internal static Dictionary<string, string> ToKeyValues(HttpCookieCollection collection, bool keysOnly)
        {
            var keys = collection.AllKeys;
            var items = new Dictionary<string, string>();

            foreach (string key in keys)
            {
                try
                {
                    HttpCookie cookie = collection[key];

                    if (cookie != null &&  !string.IsNullOrWhiteSpace(cookie.Value))
                    {
                     
                        if (keysOnly)
                        {
                            items.Add(key, "X-MASKED-X");
                        }
                        else
                        {
                            items.Add(key, cookie.Value);
                        }
                    }

                }
                catch (Exception)
                {

                }
            }

            return items;
        }

        internal static Dictionary<string, string> ToKeyValues(NameValueCollection collection, bool keysOnly, List<string> skipKeysList = null)
        {
            var keys = collection.AllKeys;
            var items = new Dictionary<string, string>();

            foreach (string key in keys)
            {
                try
                {
                    if (skipKeysList != null && skipKeysList.Contains(key.ToLower()))
                    {
                        continue;
                    }

                    string val = collection[key];

                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        if (keysOnly)
                        {
                            items.Add(key, "X-MASKED-X");
                        }
                        else
                        {
                            items.Add(key, val);
                        }
                    }

                }
                catch (Exception)
                {
                   
                }
            }

            return items;
        }


        internal static Dictionary<string, string> ToKeyValues(System.Web.SessionState.HttpSessionState collection, bool keysOnly)
        {
            var keys = collection.Keys;
            var items = new Dictionary<string, string>();

            foreach (string key in keys)
            {
                try
                {
                    object val = collection[key];

                    if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                    {
                        if (keysOnly)
                        {
                            items.Add(key, "X-MASKED-X");
                        }
                        else
                        {
                            items.Add(key, val.ToString());
                        }
                    }

                }
                catch (Exception)
                {

                }
            }

            return items;
        }
    }
}
