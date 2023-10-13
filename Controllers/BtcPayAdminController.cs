using Microsoft.AspNetCore.Mvc;
using Smartstore.ComponentModel;
using Smartstore.Core.Security;
using Smartstore.Engine.Modularity;
using Smartstore.Web.Controllers;
using Smartstore.Web.Modelling.Settings;
using Smartstore.BtcPay.Models;
using Smartstore.BtcPay.Settings;
using Smartstore.Core.Common.Services;
using Smartstore.Core;
using Autofac.Core;
using System.Web;
using Smartstore.Core.Stores;
using Newtonsoft.Json;
using static Smartstore.Core.Security.Permissions.Configuration;
using Microsoft.AspNetCore.Http;
using Org.BouncyCastle.Utilities.Collections;

namespace Smartstore.BtcPay.Controllers
{
    [Area("Admin")]
    [Route("[area]/btcpay/{action=index}/{id?}")]
    public class BtcPayAdminController : ModuleController
    {

        private readonly ICommonServices _services;
        private readonly IProviderManager _providerManager;
        private readonly ICurrencyService _currencyService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BtcPayAdminController(ICommonServices services,
                                     IHttpContextAccessor httpContextAccessor,
                                     IProviderManager providerManager,
                                     ICurrencyService currencyService)
        {
            _providerManager = providerManager;
            _httpContextAccessor = httpContextAccessor;
            _currencyService = currencyService;
            _services = services;
        }

        [LoadSetting, AuthorizeAdmin]
        public IActionResult Configure(BtcPaySettings settings)
        {
            var model = MiniMapper.Map<BtcPaySettings, ConfigurationModel>(settings);

            ViewBag.Provider = _providerManager.GetProvider("Smartstore.BTCPay").Metadata;
            ViewBag.StoreCurrencyCode = _currencyService.PrimaryCurrency.CurrencyCode ?? "EUR";
            ViewBag.UrlWebHook = _services.StoreContext.CurrentStore.Url + "BtcPayHook/Process";

            var sUrl = "";
            if (!string.IsNullOrEmpty(model.BtcPayUrl))
            {
                var myStore = _services.StoreContext.CurrentStore;
                sUrl = model.BtcPayUrl + (model.BtcPayUrl.EndsWith("/") ? "" : "/");
                sUrl += $"api-keys/authorize?applicationName={myStore.Name.Replace(" ", "")}&applicationIdentifier=SmartStore{myStore.Id}&selectiveStores=true"
                     + $"&redirect={myStore.Url}admin/btcpay/getautomaticapikeyconfig&permissions=btcpay.store.canmodifystoresettings";
            }
            ViewBag.UrlBtcApiKey = sUrl;

            return View(model);
        }

        [HttpPost, SaveSetting, AuthorizeAdmin]
        public IActionResult Configure(ConfigurationModel model, BtcPaySettings settings)
        {
            if (!ModelState.IsValid)
            {
                return Configure(settings);
            }

            ModelState.Clear();
            MiniMapper.Map(model, settings);

            return RedirectToAction(nameof(Configure));
        }


        [HttpPost, AuthorizeAdmin]
        public async Task<IActionResult> GetAutomaticApiKeyConfig()
        {
           var settings = await _services.SettingFactory.LoadSettingsAsync<BtcPaySettings>(_services.StoreContext.CurrentStore.Id);
           try
            {
                string responseStr = await new StreamReader(Request.Body).ReadToEndAsync();
                var sKey = responseStr.Split('&').First(a => a.StartsWith("apiKey")).Split('=')[1];

                settings.ApiKey = sKey;                  
            }
            catch
            {
            }
            return RedirectToAction(nameof(Configure), settings);

        }

    }
}