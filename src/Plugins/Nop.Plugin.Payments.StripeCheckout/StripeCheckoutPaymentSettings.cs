using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.StripeCheckout
{
    /// <summary>
    /// Represents settings of the StripeCheckout payment plugin
    /// </summary>
    public class StripeCheckoutPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets the api secret key
        /// </summary>
        public string ApiSecretKey { get; set; }
    }
}
