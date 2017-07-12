using System.Threading.Tasks;

namespace StackifyLib.Internal.Auth.Claims
{
    internal interface IAppClaimsBuilder
    {
        Task<AppClaims> Build();
    }
}
