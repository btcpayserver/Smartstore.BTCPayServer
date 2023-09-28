using Microsoft.AspNetCore.Mvc;
using Smartstore.ComponentModel;
using Smartstore.Core.Security;
using Smartstore.Engine.Modularity;
using Smartstore.Web.Controllers;
using Smartstore.Web.Modelling.Settings;
using Smartstore.BtcPay.Models;
using Smartstore.BtcPay.Settings;
using Smartstore.Core.Common.Services;

namespace Smartstore.BtcPay.Controllers
{
    [Area("Admin")]
    [Route("[area]/btcpay/{action=index}/{id?}")]
    public class BtcPayAdminController : ModuleController
    {

        private readonly IProviderManager _providerManager;
        private readonly ICurrencyService _currencyService;

        public BtcPayAdminController(IProviderManager providerManager, ICurrencyService currencyService)
        {
            _providerManager = providerManager;
            _currencyService = currencyService;
        }

        [LoadSetting, AuthorizeAdmin]
        public IActionResult Configure(int storeId, BtcPaySettings settings)
        {
            var model = MiniMapper.Map<BtcPaySettings, ConfigurationModel>(settings);
            ViewBag.Provider = _providerManager.GetProvider("Smartstore.BTCPay").Metadata;
            ViewBag.StoreCurrencyCode = _currencyService.PrimaryCurrency.CurrencyCode ?? "EUR";
            return View(model);
        }

        [HttpPost, SaveSetting, AuthorizeAdmin]
        public IActionResult Configure(int storeId, ConfigurationModel model, BtcPaySettings settings)
        {
            if (!ModelState.IsValid)
            {
                return Configure(storeId, settings);
            }

            ModelState.Clear();
            MiniMapper.Map(model, settings);

            return RedirectToAction(nameof(Configure));
        }

    }
}