using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.StripeCheckout.Components
{
    [ViewComponent(Name = "PaymentStripeCheckout")]
    public class PaymentStripeCheckoutViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.StripeCheckout/Views/PaymentInfo.cshtml");
        }
    }
}
