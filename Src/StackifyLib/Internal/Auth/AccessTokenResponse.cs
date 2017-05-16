using System;
using Newtonsoft.Json;

namespace StackifyLib.Auth
{
    internal class AccessTokenResponse
    {
        private readonly DateTimeOffset _createdAt;

        public AccessTokenResponse()
        {
            _createdAt = DateTimeOffset.UtcNow;
        }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        public DateTimeOffset ExpiresAt
            => _createdAt.Add(new TimeSpan(0, 0, ExpiresIn));

        public bool IsExpired() 
            => ExpiresAt <= DateTimeOffset.UtcNow;
    }
}
