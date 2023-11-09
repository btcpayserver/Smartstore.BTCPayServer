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
using Microsoft.AspNetCore.Routing;
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
        private readonly LinkGenerator _linkGenerator;
        private readonly IProviderManager _providerManager;
        private readonly ICurrencyService _currencyService;
        private readonly PaymentSettings _paymentSettings;

        public BtcPayAdminController(
            BtcPayService btcPayService,
            LinkGenerator linkGenerator,
            ICommonServices services,
            IProviderManager providerManager,
            ICurrencyService currencyService,
            PaymentSettings settings)
        {
            _btcPayService = btcPayService;
            _linkGenerator = linkGenerator;
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

            ViewBag.UrlWebHook = new Uri(new Uri(myStore.Url),
                _linkGenerator.GetPathByAction("Process", "BtcPayHook"));


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
            var adminUrl = new Uri(new Uri(myStore.Url),
                _linkGenerator.GetPathByAction(HttpContext,"GetAutomaticApiKeyConfig", "BtcPayAdmin",
                    new {ssid = myStore.Id, btcpayuri = btcpayUri}));
            var uri = BTCPayServerClient.GenerateAuthorizeUri(btcpayUri,
                new[]
                {
                    Policies.CanCreateInvoice, // create invoices for payment
                    Policies.CanViewInvoices, // fetch created invoices to check status
                    Policies.CanModifyStoreSettings, // able to mark an invoice invalid in case merchant wants to void the order
                    Policies.CanModifyStoreWebhooks, // able to create the webhook required automatically
                    Policies.CanViewStoreSettings, // able to fetch rates
                    Policies.CanCreateNonApprovedPullPayments // able to create refunds
                },
                true, true, ($"SmartStore{myStore.Id}", adminUrl));
            return uri + $"&applicationName={HttpUtility.UrlEncode(myStore.Name)}";
        }

        [HttpPost, SaveSetting, AuthorizeAdmin]
        public async Task<IActionResult> Configure(ConfigurationModel model, BtcPaySettings settings,
            string command = null)
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
            var ssid = int.Parse(ssidx.FirstOrDefault() ?? _services.StoreContext.CurrentStore.Id.ToString());
            if (ssid != _services.StoreContext.CurrentStore.Id)
            {
                return NotFound();
            }

            var settings = await _services.SettingFactory.LoadSettingsAsync<BtcPaySettings>(ssid);
            try
            {
                Request.Form.TryGetValue("apiKey", out var apiKey);
                Request.Form.TryGetValue("permissions[]", out var permissions);
                Permission.TryParse(permissions.FirstOrDefault(), out var permission);
                if (Request.Query.TryGetValue("btcpayuri", out var btcpayUris) &&
                    btcpayUris.FirstOrDefault() is { } stringbtcpayUri)
                {
                    settings.BtcPayUrl = stringbtcpayUri;
                }

                settings.ApiKey = apiKey;
                settings.BtcPayStoreID = permission.Scope;
                try
                {
                    if (permission.Scope is null)
                    {
                        settings.BtcPayStoreID = await _btcPayService.GetStoreId(settings);
                    }

                    if (string.IsNullOrEmpty(settings.WebHookSecret))
                    {
                        var webhookUrl = new Uri(new Uri(_services.StoreContext.CurrentStore.Url),
                            _linkGenerator.GetPathByAction("Process", "BtcPayHook"));
                        settings.WebHookSecret = await _btcPayService.CreateWebHook(settings, webhookUrl.ToString());
                    }
                }
                catch (Exception e)
                {
                }

                _paymentSettings.ActivePaymentMethodSystemNames.Add("Smartstore.BTCPayServer");

                await _services.SettingFactory.SaveSettingsAsync(_paymentSettings);
                await _services.SettingFactory.SaveSettingsAsync(settings);

                HttpContext.Session.SetString("ViewMsg",
                    "Settings automatically configured and payment method activated.");
            }
            catch (Exception ex)
            {
                HttpContext.Session.SetString("ViewMsgError", "Error during automatic configuration");
                Logger.Error(ex.Message);
            }

            return RedirectToAction(nameof(Configure));
        }
    }
}