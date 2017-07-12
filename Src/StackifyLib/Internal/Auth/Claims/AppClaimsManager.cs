using System.Threading.Tasks;

namespace StackifyLib.Internal.Auth.Claims
{
    internal static class AppClaimsManager
    {
        private readonly static IAppClaimsBuilder _claimsBuilder = AppClaimsBuilderFactory.Get();

        private static AppClaims _claims = null;

        public static AppClaims Get()
        {
            if (_claims != null)
                return _claims;

            GetAsync().Wait();

            return _claims;
        }

        public static async Task<AppClaims> GetAsync()
        {
            if(_claims == null)
                _claims = await _claimsBuilder.Build();

            return _claims;
        }
    }
}
