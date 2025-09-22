// Copyright (c) 2024-2025 BMC Software, Inc.
// Copyright (c) 2021-2024 Netreo
// Copyright (c) 2019 Stackify
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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
        public static IWebProxy CustomWebProxy = null;
        public static Action<HttpWebRequest> CustomRequestModifier = null;
        public static bool IsUnitTest = false;

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

            // return true if 4xx status code
            public bool IsClientError()
            {
                return (HttpStatusCode.BadRequest <= StatusCode) && (StatusCode < HttpStatusCode.InternalServerError);
            }
        }

        static HttpClient()
        {
            LoadWebProxyConfig();
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

        public static void LoadWebProxyConfig()
        {
            try
            {
                string val = Config.ProxyServer;
                if (!string.IsNullOrEmpty(val))
                {
                    StackifyAPILogger.Log("Setting proxy server based on override config", true);
                    var uri = new Uri(val);
                    WebProxy proxy = new WebProxy(uri);

                    if (!string.IsNullOrEmpty(uri.UserInfo) && uri.UserInfo.Contains(":"))
                    {
                        string[] pieces = uri.UserInfo.Split(':');
                        proxy = new WebProxy(uri)
                        {
                            Credentials = new NetworkCredential(pieces[0], pieces[1])
                        };
                    }
                    else
                    {
                        bool? settingUseDefault = Config.ProxyUseDefaultCredentials;
                        if (settingUseDefault.HasValue && settingUseDefault.Value)
                        {
                            proxy.UseDefaultCredentials = true;
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
                EnvironmentDetail env = EnvironmentDetail.Get();
                env.UpdateEnvironmentName();
                env.UpdateAppName();
                if (string.IsNullOrEmpty(env.ConfiguredAppName) && !string.IsNullOrEmpty(Config.AppName)) {
                    env.ConfiguredAppName = Config.AppName;
                }

                if (string.IsNullOrEmpty(env.ConfiguredEnvironmentName) && !string.IsNullOrEmpty(Config.Environment)) {
                    env.ConfiguredEnvironmentName = Config.Environment;
                }

                //Applicable only for Azure AppService
                if(AzureConfig.Instance.InAzure && AzureConfig.Instance.IsWebsite)
                {
                    env.DeviceName = AzureConfig.Instance.AzureInstanceName;
                }

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

#if NET40
                    response.Close();
#else
                    response.Dispose();
#endif
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

#if NET40
                        response.Close();
#else
                    response.Dispose();
#endif
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
#if NET40
                    response.Close();
#else
                    response.Dispose();
#endif
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

#if NET40
                        response.Close();
#else
                    response.Dispose();
#endif
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

            request.Headers.Add("X-Stackify-Key", this.APIKey);
            request.ContentType = "application/json";

            if (CustomWebProxy != null)
            {
                request.Proxy = CustomWebProxy;
            }

            if (CustomRequestModifier != null)
            {
                try
                {
                    CustomRequestModifier(request);
                }
                catch (Exception ex)
                {
                    StackifyAPILogger.Log($"Failed to set CustomRequestModifier - Message: {ex.Message}", true);
                }
            }

            if (!string.IsNullOrEmpty(jsonData) && compress)
            {
                request.Method = "POST";
                request.Headers.Add(HttpRequestHeader.ContentEncoding, "gzip");

                if (IsUnitTest)
                {
                    return request;
                }

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

                if (IsUnitTest)
                {
                    return request;
                }

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

            if (HttpClient.CustomWebProxy != null)
            {
                request.Proxy = HttpClient.CustomWebProxy;
            }

            if (CustomRequestModifier != null)
            {
                try
                {
                    CustomRequestModifier(request);
                }
                catch (Exception ex)
                {
                    StackifyAPILogger.Log($"Failed to set CustomRequestModifier - Message: {ex.Message}", true);
                }
            }

            request.Method = "POST";

            if (IsUnitTest)
            {
                return request;
            }

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
