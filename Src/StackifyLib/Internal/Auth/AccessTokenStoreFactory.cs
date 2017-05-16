using System.Threading.Tasks;
using System.Collections.Generic;
using StackifyLib.Http;
using StackifyLib.Internal.Auth.Claims;
using StackifyLib.Utils;
using System;

namespace StackifyLib.Auth
{
    internal sealed class AccessTokenStoreFactory
    {
        private static readonly AccessTokenStore instance = new AccessTokenStore(AuthServiceFactory.Get());

        static AccessTokenStoreFactory()
        { }

        private AccessTokenStoreFactory()
        { }

        public static AccessTokenStore Get() => instance;
    }
}
