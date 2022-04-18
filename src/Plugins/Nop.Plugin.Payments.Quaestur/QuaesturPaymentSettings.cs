using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Quaestur
{
    /// <summary>
    /// Represents settings of the Quaestur payment plugin
    /// </summary>
    public class QuaesturPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets the api client id
        /// </summary>
        public string ApiClientId { get; set; }

        /// <summary>
        /// Gets or sets the api client secret
        /// </summary>
        public string ApiClientSecret { get; set; }

        /// <summary>
        /// Gets or sets a api url
        /// </summary>
        public string ApiUrl { get; set; }
    }
}
