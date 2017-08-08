using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackifyLib.Auth;
using System.Net.Http.Headers;
using System.Net;
using System;
using System.IO.Compression;
using System.Text;
using System.IO;
using System.Net.Http;
using StackifyLib.Utils;
using System.Reflection;
using HttpClient = System.Net.Http.HttpClient;

namespace StackifyLib.Http
{
    internal class HttpRequestClient : IHttpRequestClient, IDisposable
    {
        private static readonly HttpClient _httpClient;

        static HttpRequestClient()
        {
            _httpClient = GetClient();
        }

        private static HttpClient GetClient()
        {
            var version = typeof(HttpRequestClient).GetTypeInfo().Assembly.GetName().Version;
            var userAgent = $"Stackify/{version}";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            return client;
        }

        public async Task<T> PostAsync<T>(string uri, Dictionary<string, string> formData)
        {
            StackifyAPILogger.Log("Sending POST form body");
            var data = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(uri, data);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            if (response.IsSuccessStatusCode == false)
                throw new HttpRequestException($"Received {response.StatusCode} response from {uri}");

            var content = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<T>(content);
        }

        public async Task<HttpResponseMessage> PostAsync(string uri, object json, AccessTokenResponse token, bool compress = false)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.AccessToken);

            var content = JsonConvert.SerializeObject(json, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            var shouldCompress = compress && content.Length > 5000;

            var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = GetContent(content, shouldCompress)
            };

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode == false)
                StackifyAPILogger.Log($"Received {response.StatusCode} response from {uri}");

            return response;
        }

        private HttpContent GetContent(string content, bool compress)
        {
            if (compress)
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                var ms = new MemoryStream();

                using (var gZipStream = new GZipStream(ms, CompressionLevel.Fastest, true))
                    gZipStream.Write(bytes, 0, bytes.Length);

                ms.Position = 0;
                var streamContent = new StreamContent(ms);
                streamContent.Headers.ContentEncoding.Add("gzip");
                return streamContent;
            }

            return new StringContent(content);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
