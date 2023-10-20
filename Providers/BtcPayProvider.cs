using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.BtcPay.Components;
using Smartstore.BtcPay.Controllers;
using Smartstore.BtcPay.Models;
using Smartstore.BtcPay.Services;
using Smartstore.BtcPay.Settings;
using Smartstore.Core;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Configuration;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Widgets;
using Smartstore.Engine.Modularity;
using Smartstore.Http;

namespace Smartstore.BtcPay.Providers
{
    [SystemName("Smartstore.BTCPay")]
    [FriendlyName("BTCPay")]
    [Order(1)]
    public class PaymentProvider : PaymentMethodBase, IConfigurable
    {
        // https://smartstore.atlassian.net/wiki/spaces/SMNET40/pages/1927643267/How+to+write+a+Payment+Plugin

        private readonly ICommonServices _services;
        private readonly ICustomerService _customerService;
        private readonly ILocalizationService _localizationService;
        private readonly ICurrencyService _currencyService;
        private readonly ISettingFactory _settingFactory;
        private readonly SmartDbContext _db;

        public PaymentProvider(
            ICommonServices services,
            ILocalizationService localizationService,
            ICustomerService customerService,
            ISettingFactory settingFactory,
            ICurrencyService currencyService,
            SmartDbContext db)
        {
            _localizationService = localizationService;
            _services = services;
            _currencyService = currencyService;
            _customerService = customerService;
            _settingFactory = settingFactory;
            _db = db;
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public static string SystemName => "Smartstore.BtcPay";

        /// <summary>
        /// Defines whether the payment can be catpured by this payment provider.
        /// </summary>
		public override bool SupportCapture => false;

        /// <summary>
        /// Defines whether the payment can be parially refunded by this payment provider.
        /// </summary>
		public override bool SupportPartiallyRefund => true;

        /// <summary>
        /// Defines whether the payment can be refunded by this payment provider.
        /// </summary>
		public override bool SupportRefund => true;

        /// <summary>
        /// Defines whether the payment can be voided by this payment provider.
        /// </summary>
		public override bool SupportVoid => false;

        /// <summary>
        /// Must be true when the payment method requests payment data during checkout.
        /// </summary>
        public override bool RequiresInteraction => false;

        /// <summary>
        /// For more information about supported payment types and there function make a right click on PaymentMethodType and go to definition.
        /// </summary>
        public override PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;


        public RouteInfo GetConfigurationRoute()
            => new(nameof(BtcPayAdminController.Configure), "BtcPay", new { area = "Admin" });


        public override Widget GetPaymentInfoWidget()
            => new ComponentWidget(typeof(BtcPayViewComponent));



        public override async Task<(decimal FixedFeeOrPercentage, bool UsePercentage)> GetPaymentFeeInfoAsync(ShoppingCart cart)
        {
            var settings = await _settingFactory.LoadSettingsAsync<BtcPaySettings>(_services.StoreContext.CurrentStore.Id);
            return (settings.AdditionalFee, settings.AdditionalFeePercentage);
        }

        /// <summary>
        /// Will process the payment. Is always executed after confirm order in the checkout process was clicked.
        /// </summary>
        public override async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;

            // implement process payment
            try
            {
                var myStore = _services.StoreContext.CurrentStore;
                var settings = await _settingFactory.LoadSettingsAsync<BtcPaySettings>(myStore.Id);

                string sEmail;
                string sFullName;
                Customer? myCustomer = await _customerService.GetAuthenticatedCustomerAsync();
                if (myCustomer == null)
                {
                    myCustomer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == processPaymentRequest.CustomerId) 
                                        ?? throw new Exception("Customer not found");
                    sEmail = myCustomer.BillingAddress.Email;
                    sFullName = myCustomer.BillingAddress.GetFullName(); 
                } else
                {
                    sEmail = myCustomer.Email;
                    sFullName = myCustomer.FullName;
                }
      

                BtcPayService apiService = new BtcPayService();
                result.AuthorizationTransactionResult = apiService.CreateInvoice(settings, new PaymentDataModel()
                                    {
                                        CurrencyCode = _currencyService.PrimaryCurrency.CurrencyCode,
                                        Amount = processPaymentRequest.OrderTotal,
                                        BuyerEmail = sEmail,
                                        BuyerName = sFullName,
                                        OrderID = processPaymentRequest.OrderGuid.ToString(),
                                        StoreID = myStore.Id,
                                        CustomerID = myCustomer.Id,
                                        Description = "From " + myStore.Name,
                                        RedirectionURL = myStore.Url + "checkout/completed",
                                        Lang = _services.WorkContext.WorkingLanguage.LanguageCulture
                                    }) ;
                
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                throw new PaymentException(ex.Message);
            }
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Will be called after payment is processed
        /// </summary>
        public override Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            if (postProcessPaymentRequest.Order.PaymentStatus == PaymentStatus.Pending)
            {
                // Specify redirection URL here if your provider is of type PaymentMethodType.Redirection.
                // Core redirects to this URL automatically.
                postProcessPaymentRequest.RedirectUrl = postProcessPaymentRequest.Order.AuthorizationTransactionResult;
            }
            return Task.CompletedTask;
        }


        /// <summary>
        /// When true user can reprocess payment from MyAccount > Orders > OrderDetail
        /// </summary>
        public override Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            if (order.PaymentStatus == PaymentStatus.Pending)
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// Will be executed when payment is refunded by shop admin in the backend.
        /// </summary>
        public override Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var order = refundPaymentRequest.Order;
            var result = new RefundPaymentResult
            {
                NewPaymentStatus = order.PaymentStatus
            };

            try
            {
                var myStore = _services.StoreContext.CurrentStore;
                var settings = _settingFactory.LoadSettings<BtcPaySettings>(myStore.Id);
                BtcPayService apiService = new BtcPayService();
                var sUrl = apiService.CreateRefund(settings, refundPaymentRequest);
                refundPaymentRequest.Order.AddOrderNote(_localizationService.GetResource("Plugins.Smartstore.BtcPay.NoteRefund") + sUrl, true);
                result.NewPaymentStatus = refundPaymentRequest.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                throw new PaymentException(ex.Message);
            }

            return Task.FromResult(result);
        }
    }
}