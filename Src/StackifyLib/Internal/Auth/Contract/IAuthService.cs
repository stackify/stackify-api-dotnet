using System.Threading.Tasks;
using StackifyLib.Internal.Auth.Claims;

namespace StackifyLib.Auth
{
    internal interface IAuthService
    {
        Task<AccessTokenResponse> AuthenticateAsync(AppClaims claims);
    }
}
