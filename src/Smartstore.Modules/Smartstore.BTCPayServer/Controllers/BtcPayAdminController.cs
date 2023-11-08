using Microsoft.AspNetCore.Mvc;
using Smartstore.ComponentModel;
using Smartstore.Core.Security;
using Smartstore.Engine.Modularity;
using Smartstore.Web.Controllers;
using Smartstore.Web.Modelling.Settings;
using Smartstore.BTCPayServer.Models;
using Smartstore.Core.Common.Services;
using Smartstore.Core;
using System.Web;
using BTCPayServer.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Smartstore.BTCPayServer.Configuration;
using Smartstore.BTCPayServer.Services;
using Smartstore.Core.Checkout.Payment;

namespace Smartstore.BTCPayServer.Controllers
{
    [Area("Admin")]
    [Route("[area]/btcpayserver/{action=index}/{id?}")]
    public class BtcPayAdminController : ModuleController
    {

        private readonly ICommonServices _services;
        private readonly BtcPayService _btcPayService;
        private readonly IProviderManager _providerManager;
        private readonly ICurrencyService _currencyService;
        private readonly PaymentSettings _paymentSettings;

        public BtcPayAdminController(
            BtcPayService btcPayService,
                                     ILogger<BtcPayAdminController> logger,
                                    ICommonServices services,
                                     IProviderManager providerManager,
                                     ICurrencyService currencyService,
            
            PaymentSettings settings)
        {
            _btcPayService = btcPayService;
            _providerManager = providerManager;
            _currencyService = currencyService;
            _paymentSettings = settings;
            _services = services;
        }

        [LoadSetting, AuthorizeAdmin]
        public IActionResult Configure(BtcPaySettings settings)
        {
            var model = MiniMapper.Map<BtcPaySettings, ConfigurationModel>(settings);
            var myStore = _services.StoreContext.CurrentStore;
            
            ViewBag.Provider = _providerManager.GetProvider("Smartstore.BTCPayServer").Metadata;
            ViewBag.StoreCurrencyCode = _currencyService.PrimaryCurrency.CurrencyCode ?? "EUR";
            ViewBag.UrlWebHook = myStore.Url + "BtcPayHook/Process";

            var sViewMsg = HttpContext.Session.GetString("ViewMsg");
            if (!string.IsNullOrEmpty(sViewMsg))
            {
                ViewBag.ViewMsg = sViewMsg;
                HttpContext.Session.SetString("ViewMsg", "");
            }

            var sViewMsgError = HttpContext.Session.GetString("ViewMsgError");
            if (!string.IsNullOrEmpty(sViewMsgError))
            {
                ViewBag.ViewMsgError = sViewMsgError;
                HttpContext.Session.SetString("ViewMsgError", "");
            }

            var sUrl = "";
            if (!string.IsNullOrEmpty(model.BtcPayUrl))
            {
                
                sUrl = model.BtcPayUrl + (model.BtcPayUrl.EndsWith("/") ? "" : "/");
                sUrl += $"api-keys/authorize?applicationName={myStore.Name.Replace(" ", "")}&applicationIdentifier=SmartStore{myStore.Id}&selectiveStores=true"
                     + $"&redirect={myStore.Url}admin/btcpayserver/getautomaticapikeyconfig&permissions=btcpay.store.canmodifystoresettings";
            }
            ViewBag.UrlBtcApiKey = sUrl;
            ViewBag.UrlCreateWebHook = myStore.Url + "admin/btcpayserver/createwebhook/";
            return View(model);
        }

        private string? GetRedirectUri(BtcPaySettings btcPaySettings)
        {
            if (string.IsNullOrEmpty(btcPaySettings?.BtcPayUrl) ||
                !Uri.TryCreate(btcPaySettings?.BtcPayUrl, UriKind.Absolute, out var btcpayUri))
            {
                return null;
            }

            var myStore = _services.StoreContext.CurrentStore;
            var adminUrl = myStore.Url + "admin/btcpayserver/getautomaticapikeyconfig";
            var adminUrlParams = new Dictionary<string, string>();
            adminUrlParams.Add("ssid", myStore.Id.ToString());
            adminUrlParams.Add("btcpayuri", btcpayUri.ToString());
            adminUrl += QueryString.Create(adminUrlParams);

            var uri = BTCPayServerClient.GenerateAuthorizeUri(btcpayUri, new[] {"btcpay.store.canmodifystoresettings"},
                true, true, ($"SmartStore{myStore.Id}", new Uri(adminUrl)));
            return uri + $"&applicationName={HttpUtility.UrlEncode(myStore.Name)}";
        }

        [HttpPost, SaveSetting, AuthorizeAdmin]
        public async Task<IActionResult> Configure(ConfigurationModel model, BtcPaySettings settings , string command= null)
        {
            if (command == "delete")
            {
                settings.ApiKey = "";
                settings.BtcPayStoreID = "";
                settings.WebHookSecret = "";
                settings.BtcPayUrl = "";
                ModelState.Clear();
                _paymentSettings.ActivePaymentMethodSystemNames.Remove("Smartstore.BTCPayServer");
                
                HttpContext.Session.SetString("ViewMsg", "Settings cleared and payment method deactivated");
                return Configure(settings);
            }

            if (command == "activate" && model.IsConfigured())
            {
                _paymentSettings.ActivePaymentMethodSystemNames.Add("Smartstore.BTCPayServer");
                
                await _services.SettingFactory.SaveSettingsAsync(_paymentSettings);
                
                HttpContext.Session.SetString("ViewMsg", "Payment method activated");
            }

            if (command == "getautomaticapikeyconfig")
            {
                
                MiniMapper.Map(model, settings);
                string? result = GetRedirectUri(settings);
                if (result != null)
                {
                    return Redirect(result);
                }

                HttpContext.Session.SetString("ViewMsgError",
                    "Cannot generate automatic configuration URL. Please check your BTCPay URL.");
                return Configure(settings);
            }

            if (!ModelState.IsValid)
            {
                HttpContext.Session.SetString("ViewMsgError", "Incorrect data");
                return Configure(settings);
            }

            ModelState.Clear();
            MiniMapper.Map(model, settings);

            HttpContext.Session.SetString("ViewMsg", "Save OK");
            return RedirectToAction(nameof(Configure));
        }


        //[HttpPost, AuthorizeAdmin]
        [HttpPost]
        public async Task<IActionResult> GetAutomaticApiKeyConfig()
        {
            Request.Query.TryGetValue("ssid", out var ssidx);
           var ssid  = int.Parse(ssidx.FirstOrDefault() ?? _services.StoreContext.CurrentStore.Id.ToString());
           var settings = await _services.SettingFactory.LoadSettingsAsync<BtcPaySettings>(ssid);
           try
            {
                
                Request.Form.TryGetValue("apiKey", out var apiKey);
                Request.Form.TryGetValue("permissions[]", out var permissions);
                Permission.TryParse(permissions.FirstOrDefault(), out var permission);
                if (Request.Query.TryGetValue("btcpayuri", out var btcpayUris) && btcpayUris.FirstOrDefault() is { } stringbtcpayUri)
                {
                    settings.BtcPayUrl = stringbtcpayUri;
                }
                settings.ApiKey = apiKey;
                settings.BtcPayStoreID = permission.Scope;
                try
                {
                    if (permission.Scope is null)
                    {
                        settings.BtcPayStoreID = await  _btcPayService.GetStoreId(settings);
                    }

                    if(string.IsNullOrEmpty(settings.WebHookSecret))
                    {
                        settings.WebHookSecret =  await _btcPayService.CreateWebHook(settings, _services.StoreContext.CurrentStore.Url + "BtcPayHook/Process");
                    }
                   
                }
                catch (Exception e)
                {
                }
                _paymentSettings.ActivePaymentMethodSystemNames.Add("Smartstore.BTCPayServer");
                
                await _services.SettingFactory.SaveSettingsAsync(_paymentSettings);
                await _services.SettingFactory.SaveSettingsAsync(_paymentSettings);
                
                HttpContext.Session.SetString("ViewMsg", "API Key and Store ID set with success. Don't forget to click on <b>Save</b> button to update data !");
            }
            catch (Exception ex)
            {
                HttpContext.Session.SetString("ViewMsgError", "Error during API Key creation !");
                Logger.Error(ex.Message);
            }
            return RedirectToAction(nameof(Configure), settings);

        }

        [HttpGet]
        public async Task<IActionResult> CreateWebHook()
        {
            var myStore = _services.StoreContext.CurrentStore;
            var settings = await _services.SettingFactory.LoadSettingsAsync<BtcPaySettings>(myStore.Id);

            if (! (string.IsNullOrEmpty(settings.BtcPayStoreID)
                   || string.IsNullOrEmpty(settings.BtcPayUrl)
                   || string.IsNullOrEmpty(settings.ApiKey)))
            {
                try
                {
                    settings.WebHookSecret = await _btcPayService.CreateWebHook(settings, myStore.Url + "BtcPayHook/Process");
                    HttpContext.Session.SetString("ViewMsg", "WebHook created successfully. Don't forget to click on <b>Save</b> button to update data !");
                }
                catch (Exception ex)
                {
                    HttpContext.Session.SetString("ViewMsgError", "Error during WebHook creation! Make sure your API Key and Store ID are correct.");
                    Logger.Error(ex.Message);
              }
            }
            return RedirectToAction(nameof(Configure), settings);
        }

    }
}