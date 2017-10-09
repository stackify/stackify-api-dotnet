namespace StackifyLib.Internal.Auth.Claims
{
    internal static class AppClaimsBuilderFactory
    {
        public static IAppClaimsBuilder Get()
        {
#if NETFULL
            return new AppClaimsBuilderFullFramework();
#else
            return new AppClaimsBuilderStandard();
#endif
        }
    }
}
