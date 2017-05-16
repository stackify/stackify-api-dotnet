using System.Threading.Tasks;
using StackifyLib.Internal.Auth.Claims;

namespace StackifyLib.Auth
{
    internal interface ITokenStore
    {
        Task<AccessTokenResponse> GetTokenAsync(AppClaims claims);
    }
}
