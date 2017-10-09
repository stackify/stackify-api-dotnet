using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackifyLib.Internal.Auth.Claims;

namespace StackifyLib.Utils
{
    internal class HttpClient
    {
#if NETFULL
        public static IWebProxy CustomWebProxy = null;
#endif

        public string BaseAPIUrl { get; private set; }

        public string APIKey
        {
            get
            {
                if (string.IsNullOrEmpty(Config.ApiKey))
                {
                    return _APIKey;
                }
                else
                {
                    return Config.ApiKey;
                }
            }

        }
        private string _APIKey = null;

        public AppIdentityInfo AppIdentity { get; internal set; }


        //public bool MetricAPIDisabled = false;

        private bool IdentityComplete = false;
        private DateTime _LastIdentityAttempt;
        private DateTime? _UnauthorizedResponse = null;

        private DateTime? _LastError = null;
        private DateTime? _NextTry = null;

        private DateTime? _LastSuccess = DateTime.UtcNow;

        public string LastErrorMessage = null;

        public class StackifyWebResponse
        {
            public string ResponseText { get; set; }
            public System.Net.HttpStatusCode StatusCode { get; set; }
            public Exception Exception { get; set; }
        }

        static HttpClient()
        {
#if NETFULL
            LoadWebProxyConfig();
#endif
        }

        public HttpClient(string apiKey, string apiUrl)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                _APIKey = Config.Get("Stackify.ApiKey");
            }
            else
            {
                _APIKey = apiKey;
            }

            if (string.IsNullOrEmpty(apiUrl))
            {
                string customUrl = Config.Get("Stackify.ApiUrl");

                if (!string.IsNullOrWhiteSpace(customUrl))
                {
                    BaseAPIUrl = customUrl;
                }

                if (BaseAPIUrl == null)
                    BaseAPIUrl = "https://api.stackify.com/";
            }
            else
            {
                BaseAPIUrl = apiUrl;
            }
            _LastIdentityAttempt = DateTime.UtcNow.AddMinutes(-15);

            if (BaseAPIUrl != null && !BaseAPIUrl.EndsWith("/"))
                BaseAPIUrl += "/";
        }

#if NETFULL
        public static void LoadWebProxyConfig()
        {
            try
            {
                string val = Config.Get("Stackify.ProxyServer");

                if (!string.IsNullOrEmpty(val))
                {

                    StackifyAPILogger.Log("Setting proxy server based on override config", true);

                    var uri = new Uri(val);

                    var proxy = new WebProxy(uri, false);

                    if (!string.IsNullOrEmpty(uri.UserInfo) && uri.UserInfo.Contains(":"))
                    {
                        string[] pieces = uri.UserInfo.Split(':');

                        proxy.Credentials = new NetworkCredential(pieces[0], pieces[1]);
                    }
                    else
                    {

                        string settingUseDefault = Config.Get("Stackify.ProxyUseDefaultCredentials");

                        bool useDefault;

                        if (!string.IsNullOrEmpty(settingUseDefault) && bool.TryParse(settingUseDefault, out useDefault))
                        {
                            //will make it use the user of the running windows service
                            proxy.UseDefaultCredentials = useDefault;
                        }
                    }
                    CustomWebProxy = proxy;
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("Error setting default web proxy " + ex.Message, true);
            }
        }
#endif

        /// <summary>
        /// This method does some throttling when errors happen to control when it should try again. Error backoff logic
        /// </summary>
        private void CalcNextTryOnError()
        {

            if (_LastError == null)
            {
                //let the next one go
                _NextTry = DateTime.UtcNow;
            }
            else
            {
                TimeSpan sinceLastError = DateTime.UtcNow.Subtract(_LastError.Value);

                if (sinceLastError < TimeSpan.FromSeconds(1))
                {
                    _NextTry = DateTime.UtcNow.AddSeconds(1);
                }
                else if (sinceLastError < TimeSpan.FromSeconds(2))
                {
                    _NextTry = DateTime.UtcNow.AddSeconds(2);
                }
                else if (sinceLastError < TimeSpan.FromSeconds(3))
                {
                    _NextTry = DateTime.UtcNow.AddSeconds(3);
                }
                else if (sinceLastError < TimeSpan.FromSeconds(4))
                {
                    _NextTry = DateTime.UtcNow.AddSeconds(4);
                }
                else if (sinceLastError < TimeSpan.FromSeconds(5))
                {
                    _NextTry = DateTime.UtcNow.AddSeconds(5);
                }
                else if (sinceLastError < TimeSpan.FromSeconds(10))
                {
                    _NextTry = DateTime.UtcNow.AddSeconds(10);
                }
                else if (sinceLastError < TimeSpan.FromSeconds(20))
                {
                    _NextTry = DateTime.UtcNow.AddSeconds(20);
                }
                else if (sinceLastError < TimeSpan.FromSeconds(30))
                {
                    _NextTry = DateTime.UtcNow.AddSeconds(30);
                }
                else
                {
                    _NextTry = DateTime.UtcNow.AddMinutes(1);
                }
            }

            _LastError = DateTime.UtcNow;
        }

        public bool CanUpload()
        {
            if (!IsRecentError() && IsAuthorized() && IdentifyApp())
            {
                return true;
            }

            return false;
        }

        public bool IsRecentError()
        {
            //no error
            if (_LastError == null)
            {
                return false;
            }

            //last success should really never be greater than last error, but if it is for some reason, go ahead and return false
            if (_LastSuccess > _LastError)
            {
                return false;
            }

            //If next try is set to a future time, we wait and return true
            if (_NextTry != null && DateTime.UtcNow < _NextTry)
            {
                return true;
            }
            return false;
        }

        public bool IsAuthorized()
        {
            if (_UnauthorizedResponse == null)
            {
                return true;
            }

            TimeSpan ts = DateTime.UtcNow.Subtract(_UnauthorizedResponse.Value);

            //try again if been more than 5 minutes
            return ts.TotalMinutes > 5;
        }

        public bool MatchedClientDeviceApp()
        {
            if (IdentifyApp() && this.AppIdentity != null && this.AppIdentity.DeviceAppID != null)
                return true;

            return false;
        }

        public bool IdentifyApp()
        {
            //if identify fails for some reason we can return the previous state incase it was completed before.
            bool currentIdentityStatus = IdentityComplete;
            try
            {
                int waitTime = 5; //check every 5

                //was successful before and we know the appid
                if (this.AppIdentity != null && this.AppIdentity.DeviceAppID.HasValue)
                {
                    waitTime = 15; //refresh every 15
                }


                if (_LastIdentityAttempt.AddMinutes(waitTime) > DateTime.UtcNow)
                {
                    return currentIdentityStatus;
                }

                //if we get this far that means it failed more than 5 minutes ago, is the first time, or succeeded more than 15 minutes ago


                if (string.IsNullOrEmpty(APIKey))
                {
                    StackifyAPILogger.Log("Skipping IdentifyApp(). No APIKey configured.", true);
                    return false;
                }
                StackifyAPILogger.Log("Calling to Identify App");
                var env = AppClaimsManager.Get();
                string jsonData = JsonConvert.SerializeObject(env, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

                var response =
                    SendJsonAndGetResponse(
                        (BaseAPIUrl) + "Metrics/IdentifyApp", jsonData);

                if (response.Exception == null && response.StatusCode == HttpStatusCode.OK)
                {
                    _LastIdentityAttempt = DateTime.UtcNow;

                    AppIdentity = JsonConvert.DeserializeObject<AppIdentityInfo>(response.ResponseText);

                    if (AppIdentity != null)
                    {
                        //always use whatever the configured app name is, don't just use what comes back in case they don't match
                        if (!string.IsNullOrEmpty(env.ConfiguredAppName) && env.ConfiguredAppName != AppIdentity.AppName)
                        {
                            AppIdentity.AppName = env.ConfiguredAppName;
                            AppIdentity.AppNameID = null;
                            AppIdentity.AppEnvID = null;
                        }

                        IdentityComplete = true;
                        return true;
                    }
                }

                return currentIdentityStatus;
            }
            catch (Exception ex)
            {
                _LastIdentityAttempt = DateTime.UtcNow;

                StackifyAPILogger.Log("IdentifyApp() HTTP Response Error: " + ex.ToString(), true);

                return currentIdentityStatus;
            }
        }

        public Task<StackifyWebResponse> SendJsonAndGetResponseAsync(string url, string jsonData, bool compress = false)
        {
            return AsyncWrap<StackifyWebResponse>(() => SendJsonAndGetResponse(url, jsonData, compress));
        }

        private Task<T> AsyncWrap<T>(Func<T> selector)
        {
            return Task.Factory.StartNew(selector);
        }

        public StackifyWebResponse SendJsonAndGetResponse(string url, string jsonData, bool compress = false)
        {
            if (url == null || this.APIKey == null)
            {
                StackifyAPILogger.Log("unable to send. Missing url or api key");
                return new StackifyWebResponse() { Exception = new Exception("Missing url or api key") };
            }

            if (!IsAuthorized())
            {
                StackifyAPILogger.Log("Preventing API call due to unauthorized error");
                return new StackifyWebResponse() { Exception = new Exception("unauthorized") };
            }

            StackifyAPILogger.Log("Send to " + url + " key " + this.APIKey + "\r\n" + jsonData);

            //default to 500. Should get set below.
            StackifyWebResponse result = new StackifyWebResponse() { StatusCode = HttpStatusCode.InternalServerError };
            DateTime started = DateTime.UtcNow;

            try
            {
                var request = BuildJsonRequest(url, jsonData, compress);

#if NETFULL
                using (var response = (HttpWebResponse)request.GetResponse())
#else
                using (var response = (HttpWebResponse)request.GetResponseAsync().GetAwaiter().GetResult())
#endif
                {
                    if (response == null)
                        return null;

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _UnauthorizedResponse = DateTime.UtcNow;
                    }

                    result.ResponseText = GetResponseString(response, started);
                    result.StatusCode = response.StatusCode;

                    _LastSuccess = DateTime.UtcNow;
                    _LastError = null;
                    LastErrorMessage = null;

                    response.Dispose();
                }
            }
            catch (WebException ex)
            {
                StackifyAPILogger.Log(ex.ToString());

                CalcNextTryOnError();
                result.Exception = ex;
                LastErrorMessage = ex.Message;
                if (ex.Response != null)
                {
                    HttpWebResponse response = ex.Response as HttpWebResponse;

                    if (response != null)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            _UnauthorizedResponse = DateTime.UtcNow;
                        }

                        result.StatusCode = response.StatusCode;
                        result.ResponseText = GetResponseString(response, started);

                        response.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log(ex.ToString());
                CalcNextTryOnError();
                LastErrorMessage = ex.Message;
                result.Exception = ex;
            }

            return result;
        }

        public StackifyWebResponse POSTAndGetResponse(string url, string postData)
        {
            if (url == null || this.APIKey == null)
            {
                StackifyAPILogger.Log("unable to send. Missing url or api key");
                return new StackifyWebResponse() { Exception = new Exception("Missing url or api key") };
            }

            if (!IsAuthorized())
            {
                StackifyAPILogger.Log("Preventing API call due to unauthorized error");
                return new StackifyWebResponse() { Exception = new Exception("unauthorized") };
            }

            StackifyAPILogger.Log("Send to " + url + " key " + this.APIKey + "\r\n" + postData);

            //default to 500. Should get set below.
            StackifyWebResponse result = new StackifyWebResponse() { StatusCode = HttpStatusCode.InternalServerError };
            DateTime started = DateTime.UtcNow;

            try
            {
                var request = BuildPOSTRequest(url, postData);

#if NET451 || NET45
                using (var response = (HttpWebResponse)request.GetResponse())
#else
                using (var response = (HttpWebResponse)request.GetResponseAsync().GetAwaiter().GetResult())
#endif
                {
                    if (response == null)
                        return null;

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _UnauthorizedResponse = DateTime.UtcNow;
                    }

                    result.ResponseText = GetResponseString(response, started);
                    result.StatusCode = response.StatusCode;

                    _LastSuccess = DateTime.UtcNow;
                    _LastError = null;
                    LastErrorMessage = null;
                    response.Dispose();
                }
            }
            catch (WebException ex)
            {
                StackifyAPILogger.Log(ex.ToString());

                CalcNextTryOnError();
                result.Exception = ex;
                LastErrorMessage = ex.Message;
                if (ex.Response != null)
                {
                    HttpWebResponse response = ex.Response as HttpWebResponse;

                    if (response != null)
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            _UnauthorizedResponse = DateTime.UtcNow;
                        }

                        result.StatusCode = response.StatusCode;
                        result.ResponseText = GetResponseString(response, started);

                        response.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log(ex.ToString());
                CalcNextTryOnError();
                LastErrorMessage = ex.Message;
                result.Exception = ex;
            }

            return result;
        }


        public string GetResponseString(HttpWebResponse response, DateTime started)
        {
            if (response == null)
                return null;

            try
            {

                using (var responseStream = response.GetResponseStream())
                {
                    if (responseStream == null || !responseStream.CanRead)
                        return null;

                    using (var sr = new StreamReader(responseStream))
                    {
                        string responseData = sr.ReadToEnd();
                        long took = (long)DateTime.UtcNow.Subtract(started).TotalMilliseconds;

                        bool forceLog = ((int)response.StatusCode) > 400;

                        StackifyAPILogger.Log("GetResponseString HTTP Response: " + ((int)response.StatusCode).ToString() + ", Took: " + took + "ms - " + responseData + " " + response.ResponseUri.ToString(), forceLog);
                        return responseData;
                    }
                }

            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("HTTP Response Error: " + ex.ToString() + " " + response.ResponseUri.ToString(), true);
                LastErrorMessage = ex.Message;
                CalcNextTryOnError();
                return null;
            }
        }

        private string _version = null;

        private HttpWebRequest BuildJsonRequest(string url, string jsonData, bool compress)
        {
            if (string.IsNullOrEmpty(_version))
            {

#if NETFULL
                _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
#else
                _version =
                    typeof(HttpClient).GetTypeInfo()
                        .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                        .InformationalVersion;
#endif
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

#if NETFULL
            request.UserAgent = "StackifyLib-" + _version;
#else
            request.Headers[HttpRequestHeader.UserAgent] = "StackifyLib-" + _version;
#endif

            request.Headers["X-Stackify-Key"] = this.APIKey;
            request.ContentType = "application/json";

            //if (HttpClient.CustomWebProxy != null)
            //{

            //    request.Proxy = HttpClient.CustomWebProxy;
            //}

            if (!string.IsNullOrEmpty(jsonData) && compress)
            {
                request.Method = "POST";
                request.Headers[HttpRequestHeader.ContentEncoding] = "gzip";

                byte[] payload = Encoding.UTF8.GetBytes(jsonData);

#if NETFULL
                using (Stream postStream = request.GetRequestStream())
#else
                using (Stream postStream = request.GetRequestStreamAsync().GetAwaiter().GetResult())
#endif
                {
                    using (var zipStream = new GZipStream(postStream, CompressionMode.Compress))
                    {
                        zipStream.Write(payload, 0, payload.Length);
                    }
                }

            }
            else if (!string.IsNullOrEmpty(jsonData))
            {
                request.Method = "POST";

                byte[] payload = Encoding.UTF8.GetBytes(jsonData);

#if NETFULL
                request.ContentLength= payload.Length;
                using (Stream stream = request.GetRequestStream())
#else
                request.Headers[HttpRequestHeader.ContentLength] = payload.Length.ToString();
                using (Stream stream = request.GetRequestStreamAsync().GetAwaiter().GetResult())
#endif
                {
                    stream.Write(payload, 0, payload.Length);
                }
            }
            else
            {
                request.Method = "GET";
            }

            return request;
        }


        private HttpWebRequest BuildPOSTRequest(string url, string postdata)
        {
            if (string.IsNullOrEmpty(_version))
            {
#if NETFULL
                _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
#else
                _version =
                    typeof(HttpClient).GetTypeInfo()
                        .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                        .InformationalVersion;
#endif
            }

            var request = (HttpWebRequest)WebRequest.Create(url);


            request.Headers["X-Stackify-Key"] = this.APIKey;
            request.ContentType = "application/x-www-form-urlencoded";

#if NETFULL
            request.UserAgent = "StackifyLib-" + _version;
            request.ContentLength = 0;
#else
            request.Headers[HttpRequestHeader.UserAgent] = "StackifyLib-" + _version;
            request.Headers[HttpRequestHeader.ContentLength] = "0";
#endif

            //if (HttpClient.CustomWebProxy != null)
            //{
            //    request.Proxy = HttpClient.CustomWebProxy;
            //}


            request.Method = "POST";
            
            if (!String.IsNullOrEmpty(postdata))
            {
                byte[] payload = Encoding.UTF8.GetBytes(postdata);

#if NETFULL
                using (Stream postStream = request.GetRequestStream())
#else
                using (Stream postStream = request.GetRequestStreamAsync().GetAwaiter().GetResult())
#endif
                {
                    postStream.Write(payload, 0, payload.Length);
                }
            }


            return request;
        }

    }
}
