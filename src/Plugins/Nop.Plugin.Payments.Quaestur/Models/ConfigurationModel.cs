using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.Quaestur.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Quaestur.Fields.ApiClientId")]
        public string ApiClientId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Quaestur.Fields.ApiClientSecret")]
        public string ApiClientSecret { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Quaestur.Fields.ApiUrl")]
        public string ApiUrl { get; set; }
    }
}
