using System;
using System.Collections.Generic;
using System.Net;
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
        private readonly TimeSpan _errorBackoffDuration = new TimeSpan(0, 1, 0);
        private AccessTokenResponse _accessToken;

        public StackifyApiService(ITokenStore tokenStore, IHttpRequestClient httpClient)
        {
            _tokenStore = tokenStore;
            _httpClient = httpClient;
        }

        public async Task<HttpStatusCode> UploadAsync(AppClaims claims, string uri, Identifiable data, bool compress = false)
        {
            if (CanUpload() == false || data == null)
                return 0;

            if (_accessToken == null && await GetTokenAsync(claims) == false)
                return HttpStatusCode.Unauthorized;

            var url = BuildUrl(uri);

            data.AccessToken = _accessToken.AccessToken;

            var dataToPost = new List<Identifiable> { data };

            var response = await _httpClient.PostAsync(url, dataToPost, _accessToken, compress);
            if (response.IsSuccessStatusCode)
            {
                _consecutiveErrorCount = 0;
            }
            else
            {
                HandleFailedResponse(response.StatusCode);
            }
            return response.StatusCode;
        }

        private void HandleFailedResponse(HttpStatusCode status)
        {
            if (status == HttpStatusCode.Unauthorized)
            {
                _accessToken = null;
            }
            else
            {
                _consecutiveErrorCount += 1;
                _lastError = DateTimeOffset.UtcNow;
            }
        }

        public bool CanUpload()
        {
            return _claimsRejected == false && IsInErrorBackoff() == false;
        }

        private bool IsInErrorBackoff()
        {
            if (_lastError == null)
                return false;

            var backoffExpiration = _lastError.Value + _errorBackoffDuration;

            return (DateTime.UtcNow < backoffExpiration);
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
