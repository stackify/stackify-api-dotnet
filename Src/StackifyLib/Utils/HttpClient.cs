using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StackifyLib.Models;
using Newtonsoft.Json;

namespace StackifyLib.Utils
{
    public class AppIdentityInfo
    {
        public int? DeviceID { get; set; }
        public int? DeviceAppID { get; set; }
        public Guid? AppNameID { get; set; }
        public short? EnvID { get; set; }
        public string AppName { get; set; }
        public string Env { get; set; }
        public Guid? AppEnvID { get; set; }
        public string DeviceAlias { get; set; }
    }

    public class HttpClient
    {
        public string BaseAPIUrl { get; private set; }

        private string APIKey
        {
            get
            {
                if (string.IsNullOrEmpty(Logger.GlobalApiKey))
                {
                    return _APIKey;
                }
                else
                {
                    return Logger.GlobalApiKey;
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

        public HttpClient(string apiKey, string apiUrl)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                _APIKey = ConfigurationManager.AppSettings["Stackify.ApiKey"];
            }
            else
            {
                _APIKey = apiKey;
            }

            if (string.IsNullOrEmpty(apiUrl))
            {
                string customUrl = ConfigurationManager.AppSettings["Stackify.ApiUrl"];

                if (customUrl != null)
                {
                    Uri outuri;
                    if (Uri.TryCreate(customUrl, UriKind.Absolute, out outuri))
                    {
                        BaseAPIUrl = System.Web.VirtualPathUtility.AppendTrailingSlash(outuri.ToString());
                    }
                }

                if (BaseAPIUrl == null)
                {
                    //To account for our initial release which had a URL just for the error module
                    string workaround = ConfigurationManager.AppSettings["Stackify.Url"];

                    if (workaround != null)
                    {
                        workaround = System.Web.VirtualPathUtility.RemoveTrailingSlash(workaround);

                        int findIt = workaround.IndexOf("Error/V1", StringComparison.InvariantCultureIgnoreCase);

                        if (findIt > 0)
                        {
                            workaround = workaround.Substring(0, findIt);
                            BaseAPIUrl = System.Web.VirtualPathUtility.AppendTrailingSlash(workaround);
                        }
                    }
                }

                if (BaseAPIUrl == null)
                    BaseAPIUrl = "https://api.stackify.com/";
            }
            else
            {
                BaseAPIUrl = apiUrl;
            }
            _LastIdentityAttempt = DateTime.UtcNow.AddMinutes(-15);
        }
     
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

        public bool CanSend()
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
                EnvironmentDetail env = EnvironmentDetail.Get(true);
                string jsonData = JsonConvert.SerializeObject(env, new JsonSerializerSettings() {NullValueHandling = NullValueHandling.Ignore});

                var response =
                    SendAndGetResponse(
                        System.Web.VirtualPathUtility.AppendTrailingSlash(BaseAPIUrl) + "Metrics/IdentifyApp", jsonData);

                if (response.Exception == null && response.StatusCode == HttpStatusCode.OK)
                {
                    _LastIdentityAttempt = DateTime.UtcNow;

                    AppIdentity = JsonConvert.DeserializeObject<AppIdentityInfo>(response.ResponseText);

                    if (AppIdentity != null)
                    {
                        IdentityComplete = true;
                        return true;
                    }
                }

                return currentIdentityStatus;
            }
            catch (Exception ex)
            {
                _LastIdentityAttempt = DateTime.UtcNow;

                StackifyAPILogger.Log("HTTP Response Error: " + ex.ToString(), true);

                return currentIdentityStatus;
            }
        }

        public Task<StackifyWebResponse> SendAndGetResponseAsync(string url, string jsonData, bool compress = false)
        {
            return AsyncWrap<StackifyWebResponse>(() => SendAndGetResponse(url, jsonData, compress));
        }

        private Task<T> AsyncWrap<T>(Func<T> selector)
        {
            return Task.Factory.StartNew(selector);
        }

        public StackifyWebResponse SendAndGetResponse(string url, string jsonData, bool compress = false)
        {
            if (url == null || this.APIKey == null)
            {
                StackifyAPILogger.Log("unable to send. Missing url or api key");
                return new StackifyWebResponse() { Exception = new ApplicationException("Missing url or api key") };
            }

            if (!IsAuthorized())
            {
                StackifyAPILogger.Log("Preventing API call due to unauthorized error");
                return new StackifyWebResponse() { Exception = new ApplicationException("Missing url or api key") };
            }

            StackifyAPILogger.Log("Send to " + url + " key " + this.APIKey + "\r\n" + jsonData);

            //default to 500. Should get set below.
            StackifyWebResponse result = new StackifyWebResponse() { StatusCode = HttpStatusCode.InternalServerError };
            DateTime started = DateTime.UtcNow;

            try
            {
                var request = BuildWebRequest(url, jsonData, compress);

                using (var response = (HttpWebResponse)request.GetResponse())
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

                    response.Close();
                }
            }
            catch (WebException ex)
            {
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

                        response.Close();
                    }
                }
            }
            catch (Exception ex)
            {
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

                        bool forceLog = ((int) response.StatusCode) > 400;

                        StackifyAPILogger.Log("HTTP Response: " + ((int)response.StatusCode).ToString() + ", Took: " + took + "ms - " + responseData, forceLog);
                        return responseData;
                    }
                }

            }
            catch (Exception ex)
            {
                StackifyAPILogger.Log("HTTP Response Error: " + ex.ToString(), true);
                LastErrorMessage = ex.Message;
                CalcNextTryOnError();
                return null;
            }
        }

        private string _version = null;

        private HttpWebRequest BuildWebRequest(string url, string jsonData, bool compress)
        {
            if (string.IsNullOrEmpty(_version))
            {
                _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }

            var request = (HttpWebRequest)WebRequest.Create(url);
            

            request.Headers.Add("X-Stackify-Key", this.APIKey);
            request.Headers.Add("X-Stackify-PV", "V1");
            request.ContentType = "application/json";
            request.KeepAlive = false;
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            request.UserAgent = "StackifyLib-" + _version;
            
            if (!string.IsNullOrEmpty(jsonData) && compress)
            {
                request.Method = "POST";
                request.Headers.Add(HttpRequestHeader.ContentEncoding, "gzip");

                byte[] payload = Encoding.UTF8.GetBytes(jsonData);

                using (Stream postStream = request.GetRequestStream())
                {
                    using (var zipStream = new GZipStream(postStream, CompressionMode.Compress))
                    {
                        zipStream.Write(payload, 0, payload.Length);
                    }
                }

            }
            else if(!string.IsNullOrEmpty(jsonData))
            {
                request.Method = "POST";

                byte[] payload = Encoding.UTF8.GetBytes(jsonData);
                request.ContentLength = payload.Length;

                using (var stream = request.GetRequestStream())
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

    }
}
