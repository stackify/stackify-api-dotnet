namespace StackifyLib.Internal.Auth.Claims
{
    internal static class AppClaimsBuilderFactory
    {
        public static IAppClaimsBuilder Get()
        {
#if NET451 || NET45
            return new AppClaimsBuilderFullFramework();
#else
            return new AppClaimsBuilderStandard();
#endif
        }
    }
}
