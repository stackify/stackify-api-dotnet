using StackifyLib.Auth;
using StackifyLib.Http;

namespace StackifyLib.Internal.StackifyApi
{
    internal static class StackifyApiServiceFactory
    {
        public static IStackifyApiService Get()
        {
            var tokenStore = AccessTokenStoreFactory.Get();
            var httpClient = new HttpRequestClient();
            return new StackifyApiService(tokenStore, httpClient);
        }
    }
}
