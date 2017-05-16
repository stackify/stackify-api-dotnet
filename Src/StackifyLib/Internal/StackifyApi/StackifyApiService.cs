using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackifyLib.Auth;
using StackifyLib.Http;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Models;
using StackifyLib.Utils;

namespace StackifyLib.Internal.StackifyApi
{
    internal class StackifyApiService : IStackifyApiService
    {
        private readonly ITokenStore _tokenStore;
        private readonly IHttpRequestClient _httpClient;        
        private bool _claimsRejected = false;
        private DateTimeOffset? _lastError;
        private int _consecutiveErrorCount;
        private readonly TimeSpan _maxBackoffTime = new TimeSpan(0, 1, 0);
        private AccessTokenResponse _accessToken;        

        public StackifyApiService(ITokenStore tokenStore, IHttpRequestClient httpClient)
        {
            _tokenStore = tokenStore;
            _httpClient = httpClient; 
        }

        public async Task<bool> UploadAsync(AppClaims claims, string uri, Identifiable data, bool compress = false)
        {
            if (CanUpload() == false || data == null)
                return false;

            if (_accessToken == null && await GetTokenAsync(claims) == false)
                return false;

            var url = BuildUrl(uri);

            data.AccessToken = _accessToken.AccessToken;

            var dataToPost = new List<Identifiable> { data };

            try {
                 await _httpClient.PostAsync(url, dataToPost, _accessToken);
            }
            catch (UnauthorizedAccessException)
            {
                StackifyAPILogger.Log("Received 401 from API.");
                _accessToken = null;
                return false;
            }
            catch (Exception)
            {
                _consecutiveErrorCount += 1;
                _lastError = DateTimeOffset.UtcNow;
                return false;
            }

            _consecutiveErrorCount = 0;

            return true;
        }

        public bool CanUpload()
        {
            return _claimsRejected == false && IsInErrorBackoff() == false;
        }

        private bool IsInErrorBackoff()
        {
            if (_lastError == null)
                return false;

            var backoffTime = GetExponentialBackoffTime();
            var backoffExpiration = _lastError.Value + backoffTime;

            return (DateTime.UtcNow < backoffExpiration);
        }

        private TimeSpan GetExponentialBackoffTime()
        {
            var backoffSeconds = Convert.ToInt32(Math.Exp(_consecutiveErrorCount));

            var backoffTime = backoffSeconds > _maxBackoffTime.Seconds
                ? _maxBackoffTime
                : new TimeSpan(0, 0, backoffSeconds);

            return backoffTime;
        }

        private async Task<bool> GetTokenAsync(AppClaims claims)
        {
            try
            {
                _accessToken = await _tokenStore.GetTokenAsync(claims);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                StackifyAPILogger.Log("Failed to authenticate the application.");
                _claimsRejected = true;
                return false;
            }
        }

        private string BuildUrl(string uri) 
            => $"{ Config.ApiHost }/{ uri.TrimStart('/') }";
    }
}
