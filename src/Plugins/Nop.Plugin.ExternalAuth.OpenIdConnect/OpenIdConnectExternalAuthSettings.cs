using Nop.Core.Configuration;

namespace Nop.Plugin.ExternalAuth.OpenIdConnect
{
    /// <summary>
    /// Represents settings of the OpenIdConnect authentication method
    /// </summary>
    public class OpenIdConnectExternalAuthSettings : ISettings
    {
        /// <summary>
        /// Gets or sets OAuth2 client identifier
        /// </summary>
        public string ClientKeyIdentifier { get; set; }

        /// <summary>
        /// Gets or sets OAuth2 client secret
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets OAuth2 authority url
        /// </summary>
        public string AuthorityUrl { get; set; }
    }
}
