using Microsoft.AspNetCore.Mvc;
using Smartstore.Web.Components;

namespace Smartstore.BTCPayServer.Components
{
    public class BtcPayViewComponent : SmartViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Modules/SmartStore.BTCPayServer/Views/Public/PaymentInfo.cshtml");
        }
    }
}
