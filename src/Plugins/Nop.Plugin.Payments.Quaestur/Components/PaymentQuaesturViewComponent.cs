using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Quaestur.Components
{
    [ViewComponent(Name = "PaymentQuaestur")]
    public class PaymentQuaesturViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Quaestur/Views/PaymentInfo.cshtml");
        }
    }
}
