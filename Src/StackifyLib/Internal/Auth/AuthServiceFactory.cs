using StackifyLib.Http;

namespace StackifyLib.Auth
{
    internal static class AuthServiceFactory
    {
        public static IAuthService Get()
        {
            var httpClient = new HttpRequestClient();
            return new AuthService(httpClient);
        }
    }
}
