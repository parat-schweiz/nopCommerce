using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.StripeCheckout
{
	/// <summary>
	/// Represents StripeCheckout helper
	/// </summary>
	public class StripeCheckoutHelper
	{
		#region Properties

		public static string OrderAttributeStripeCheckoutSessionId => "StripeCheckoutSessionId";

		#endregion
	}
}
