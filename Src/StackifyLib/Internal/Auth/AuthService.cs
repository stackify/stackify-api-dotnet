using System.Threading.Tasks;
using System.Collections.Generic;
using StackifyLib.Http;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Utils;

namespace StackifyLib.Auth
{
    internal class AuthService : IAuthService
    {
        private string _clientId { get => Config.ApiKey; }
        private readonly IHttpRequestClient _httpClient;

        public AuthService(IHttpRequestClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<AccessTokenResponse> AuthenticateAsync(AppClaims claims)
        {
            StackifyAPILogger.Log("Requesting Access Token.");

            var body = GetClaimsRequestBody(claims);
            
            var response = await _httpClient.PostAsync<AccessTokenResponse>(Config.AuthTokenUrl, body);
         
            StackifyAPILogger.Log($"Authentication successful.");
            return response;
        }

        private Dictionary<string, string> GetClaimsRequestBody(AppClaims claims)
        {
            return new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "scope", "api" },
                { "app_name", claims.AppNameToUse() },
                { "env_name", claims.ConfiguredEnvironmentName },
                { "file_path", claims.AppLocation },
                { "azure_worker_role", claims.IsAzureWorkerRole.ToString() },
                { "device_name", claims.DeviceName },
                { "web_app_id", claims.WebAppID },
                { "client_id", _clientId },
                { "plat", claims.Platform }
            };
        }
    }
}
