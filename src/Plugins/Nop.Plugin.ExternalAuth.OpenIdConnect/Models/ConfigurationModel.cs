using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.OpenIdConnect.Models
{
    /// <summary>
    /// Represents plugin configuration model
    /// </summary>
    public record ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.ExternalAuth.OpenIdConnect.ClientKeyIdentifier")]
        public string ClientId { get; set; }

        [NopResourceDisplayName("Plugins.ExternalAuth.OpenIdConnect.ClientSecret")]
        public string ClientSecret { get; set; }

        [NopResourceDisplayName("Plugins.ExternalAuth.OpenIdConnect.AuthorityUrl")]
        public string AuthorityUrl { get; set; }
    }
}
