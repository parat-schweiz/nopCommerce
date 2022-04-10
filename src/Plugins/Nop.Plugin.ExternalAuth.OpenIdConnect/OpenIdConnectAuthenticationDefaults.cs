namespace Nop.Plugin.ExternalAuth.OpenIdConnect
{
    /// <summary>
    /// Represents plugin constants
    /// </summary>
    public class OpenIdConnectAuthenticationDefaults
    {
        /// <summary>
        /// Gets a name of the view component to display login button
        /// </summary>
        public const string VIEW_COMPONENT_NAME = "OpenIdConnectAuthentication";

        /// <summary>
        /// Gets a plugin system name
        /// </summary>
        public static string SystemName = "ExternalAuth.OpenIdConnect";

        /// <summary>
        /// Gets a name of error callback method
        /// </summary>
        public static string ErrorCallback = "ErrorCallback";

        public static string SigninCallbackPath => "/signin-oauth2";
    }
}
