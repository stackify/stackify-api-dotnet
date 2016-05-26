//using System.Runtime.Serialization;
//using StackifyLib.Web;
//using System;
//using System.Collections.Generic;
//using System.Collections.Specialized;
//using System.IO;
//using System.Linq;
//using System.Web;
//using System.Web.Routing;

//namespace StackifyLib.Models
//{
//    [DataContract]
//    public class WebRequestDetail
//    {
//        private StackifyError _Error;
//        public WebRequestDetail(StackifyError error)
//        {
//            _Error = error;
//            if (System.Web.HttpContext.Current != null)
//            {
//                Load(System.Web.HttpContext.Current);
//            }
//        }

//        [DataMember]
//        public string UserIPAddress { get; set; }

//        [DataMember]
//        public string HttpMethod { get; set; }

//        [DataMember]
//        public string RequestProtocol { get; set; }

//        [DataMember]
//        public string RequestUrl { get; set; }
//        [DataMember]
//        public string RequestUrlRoot { get; set; }

//        [DataMember]
//        public string ReportingUrl { get; set; }


//        [DataMember]
//        public string ReferralUrl { get; set; }

//        [DataMember]
//        public Dictionary<string, string> Headers { get; set; }

//        [DataMember]
//        public Dictionary<string, string> Cookies { get; set; }

//        [DataMember]
//        public Dictionary<string, string> QueryString { get; set; }

//        [DataMember]
//        public Dictionary<string, string> PostData { get; set; }

//        [DataMember]
//        public Dictionary<string, string> SessionData { get; set; }

//        [DataMember]
//        public string PostDataRaw { get; set; }

//        [DataMember]
//        public string MVCAction { get; set; }

//        [DataMember]
//        public string MVCController { get; set; }

//        [DataMember]
//        public string MVCArea { get; set; }

//        private void Load(HttpContext context)
//        {
//            if (context == null || context.Request == null)
//                return;

//            HttpRequest request = context.Request;
            
//            try
//            {
//                HttpMethod = request.RequestType;
//                UserIPAddress = request.UserHostAddress;

//                if (context.Items != null && context.Items.Contains("Stackify.ReportingUrl"))
//                {
//                    ReportingUrl = context.Items["Stackify.ReportingUrl"].ToString();
//                }

//                //We be nice to detect if it is a web sockets connection and denote that in the protocol field
//                //do we care about things like HTTP/1.1 or the port?
//                if (request.IsSecureConnection)
//                {
//                    RequestProtocol = "https";
//                }
//                else
//                {
//                    RequestProtocol = "http";
//                }

//                if (request.Url != null)
//                {
//                    RequestUrl = request.Url.ToString();
//                }

//                if (request.AppRelativeCurrentExecutionFilePath != null)
//                {
//                    RequestUrlRoot = request.AppRelativeCurrentExecutionFilePath.TrimStart('~');
//                }

//                RouteResolver resolver = new RouteResolver(context);

//                var route = resolver.GetRoute();

//                MVCArea = route.Area;
//                MVCController = route.Controller;
//                MVCAction = route.Action;

//                if (string.IsNullOrEmpty(ReportingUrl) && route != null && !string.IsNullOrEmpty(route.Action))
//                {
//                    ReportingUrl = route.ToString();
//                }
//            }
//            catch (Exception)
//            {

//            }

           

//            try
//            {
//                if (request.QueryString != null)
//                {
//                    QueryString = ToKeyValues(request.QueryString, null, null);
//                }

//                if (request.ServerVariables != null && Config.CaptureServerVariables)
//                {
//                    List<string> badKeys = new List<string>();
//                    badKeys.AddRange(new string[] { "all_http", "all_raw", "http_cookie" });

//                    var serverVars = ToKeyValues(request.ServerVariables,  null, badKeys);
//                    foreach (var serverVar in serverVars)
//                    {
//                        _Error.ServerVariables[serverVar.Key] = serverVar.Value;
//                    }
//                }

//                if (request.Headers != null && Config.CaptureErrorHeaders)
//                {
//                    if (Config.ErrorHeaderBadKeys == null)
//                    {
//                        Config.ErrorHeaderBadKeys = new List<string>();
//                    }

//                    if (!Config.ErrorHeaderBadKeys.Contains("cookie"))
//                    {
//                        Config.ErrorHeaderBadKeys.Add("cookie");
//                    }

//                    Headers = ToKeyValues(request.Headers, Config.ErrorHeaderGoodKeys, Config.ErrorHeaderBadKeys);
//                }

//                if (request.Cookies != null && Config.CaptureErrorCookies)
//                {
//                    Cookies = ToKeyValues(request.Cookies, Config.ErrorCookiesGoodKeys, Config.ErrorCookiesBadKeys);
//                }

//                if (request.Form != null && Config.CaptureErrorPostdata)
//                {
//                    PostData = ToKeyValues(request.Form,null, null);
//                }

//                if (context.Session != null && Config.CaptureSessionVariables && Config.ErrorSessionGoodKeys.Any())
//                {
//                    SessionData = ToKeyValues(context.Session, Config.ErrorSessionGoodKeys, null);
//                }

//                if (Config.CaptureErrorPostdata)
//                {
//                    var contentType = context.Request.Headers["Content-Type"];

//                    if (contentType != "text/html" && contentType != "application/x-www-form-urlencoded" &&
//                        context.Request.RequestType != "GET")
//                    {
//                        int length = 4096;
//                        string postBody = new StreamReader(context.Request.InputStream).ReadToEnd();
//                        if (postBody.Length < length)
//                        {
//                            length = postBody.Length;
//                        }

//                        PostDataRaw = postBody.Substring(0, length);
//                    }
//                }
//            }
//            catch (Exception)
//            {
//            }
//        }

//        internal static void AddKey(string key, string value, Dictionary<string, string> dictionary, List<string> goodKeys, List<string> badKeys)
//        {
//            //is this key in the bad key list?
//            if (badKeys != null && badKeys.Any(x => x.Equals(key, StringComparison.CurrentCultureIgnoreCase)))
//            {
//                dictionary[key] = "X-MASKED-X";
//                return;
//            }
//            //if not in the good key list, return
//            //if good key list is empty, we let it take it
//            else if (goodKeys != null && goodKeys.Any() && !goodKeys.Any(x => x.Equals(key, StringComparison.CurrentCultureIgnoreCase)))
//            {
//                return;
//            }

//            dictionary[key] = value;
//        }

//        internal static Dictionary<string, string> ToKeyValues(HttpCookieCollection collection, List<string> goodKeys, List<string> badKeys)
//        {
//            var keys = collection.AllKeys;
//            var items = new Dictionary<string, string>();

//            foreach (string key in keys)
//            {
//                try
//                {
//                    HttpCookie cookie = collection[key];

//                    if (cookie != null &&  !string.IsNullOrWhiteSpace(cookie.Value) && !items.ContainsKey(key))
//                    {
//                         AddKey(key, cookie.Value, items, goodKeys, badKeys);
//                    }
//                }
//                catch (Exception)
//                {

//                }
//            }

//            return items;
//        }

//        internal static Dictionary<string, string> ToKeyValues(NameValueCollection collection, List<string> goodKeys, List<string> badKeys)
//        {
//            var keys = collection.AllKeys;
//            var items = new Dictionary<string, string>();

//            foreach (string key in keys)
//            {
//                try
//                {
//                    string val = collection[key];
//                    AddKey(key, val, items, goodKeys, badKeys);
//                }
//                catch (Exception)
//                {
                   
//                }
//            }

//            return items;
//        }


//        internal static Dictionary<string, string> ToKeyValues(System.Web.SessionState.HttpSessionState collection, List<string> goodKeys, List<string> badKeys)
//        {
//            var keys = collection.Keys;
//            var items = new Dictionary<string, string>();

//            foreach (string key in keys)
//            {
//                try
//                {
//                    object val = collection[key];

//                    if (val != null && !string.IsNullOrWhiteSpace(val.ToString()) && items.ContainsKey(key))
//                    {
//                        AddKey(key, val.ToString(), items, goodKeys, badKeys);
//                    }

//                }
//                catch (Exception)
//                {

//                }
//            }

//            return items;
//        }
//    }
//}
