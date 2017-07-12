using System.Threading.Tasks;
using System.Collections.Generic;
using StackifyLib.Http;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Utils;
using System;
using System.Collections.Concurrent;

namespace StackifyLib.Auth
{
    internal class AccessTokenStore : ITokenStore
    {
        private readonly IAuthService _authService;
        private readonly ConcurrentDictionary<AppClaims, AccessTokenResponse> _tokens = new ConcurrentDictionary<AppClaims, AccessTokenResponse>();

        public AccessTokenStore(IAuthService authService)
        { 
            _authService = authService;
        }

        public async Task<AccessTokenResponse> GetTokenAsync(AppClaims claims)
        {
            if(_tokens.TryGetValue(claims, out AccessTokenResponse currentClaims) && currentClaims?.IsExpired() == false)
                return currentClaims;

            var newToken = await _authService.AuthenticateAsync(claims);
            
            _tokens[claims] = newToken;
            
            return newToken;
        }
    }
}
