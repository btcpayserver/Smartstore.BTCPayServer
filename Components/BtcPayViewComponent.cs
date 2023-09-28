using Microsoft.AspNetCore.Mvc;
using Smartstore.Web.Components;

namespace Smartstore.BtcPay.Components
{
    public class BtcPayViewComponent : SmartViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Modules/SmartStore.BtcPay/Views/Public/PaymentInfo.cshtml");
        }
    }
}
