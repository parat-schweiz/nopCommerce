using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.StripeCheckout.Infrastructure
{
	public partial class RouteProvider : IRouteProvider
	{
		/// <summary>
		/// Register routes
		/// </summary>
		/// <param name="endpointRouteBuilder">Route builder</param>
		public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
		{
			endpointRouteBuilder.MapControllerRoute("Plugin.Payments.StripeCheckout.PayedHandler", "Plugins/PaymentStripeCheckout/Payed",
				new { controller = "PaymentStripeCheckout", action = "PayedHandler" });

			endpointRouteBuilder.MapControllerRoute("Plugin.Payments.StripeCheckout.CanceledHandler", "Plugins/PaymentStripeCheckout/Canceled",
				new { controller = "PaymentStripeCheckout", action = "CanceledHandler" });
		}

		/// <summary>
		/// Gets a priority of route provider
		/// </summary>
		public int Priority => -1;
	}
}
