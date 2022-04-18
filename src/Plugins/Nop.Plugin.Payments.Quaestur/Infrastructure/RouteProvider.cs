using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Quaestur.Infrastructure
{
	public partial class RouteProvider : IRouteProvider
	{
		/// <summary>
		/// Register routes
		/// </summary>
		/// <param name="endpointRouteBuilder">Route builder</param>
		public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
		{
			endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Quaestur.PayedHandler", "Plugins/PaymentQuaestur/Payed",
				new { controller = "PaymentQuaestur", action = "PayedHandler" });

			endpointRouteBuilder.MapControllerRoute("Plugin.Payments.Quaestur.CanceledHandler", "Plugins/PaymentQuaestur/Canceled",
				new { controller = "PaymentQuaestur", action = "CanceledHandler" });
		}

		/// <summary>
		/// Gets a priority of route provider
		/// </summary>
		public int Priority => -1;
	}
}
