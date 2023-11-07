using BTCPayServer.Client.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Smartstore.BTCPayServer.Components;
using Smartstore.BTCPayServer.Configuration;
using Smartstore.BTCPayServer.Controllers;
using Smartstore.BTCPayServer.Models;
using Smartstore.BTCPayServer.Services;
using Smartstore.Core;
using Smartstore.Core.Checkout.Cart;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Configuration;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Localization;
using Smartstore.Core.Widgets;
using Smartstore.Engine.Modularity;
using Smartstore.Http;

namespace Smartstore.BTCPayServer.Providers
{
    [SystemName("Smartstore.BTCPayServer")]
    [FriendlyName("BTCPayServer")]
    [Order(1)]
    public class BTCPayPaymentProvider : PaymentMethodBase, IConfigurable
    {
        // https://smartstore.atlassian.net/wiki/spaces/SMNET40/pages/1927643267/How+to+write+a+Payment+Plugin

        private readonly ICommonServices _services;
        private readonly ICustomerService _customerService;
        private readonly ILocalizationService _localizationService;
        private readonly ICurrencyService _currencyService;
        private readonly ISettingFactory _settingFactory;
        private readonly SmartDbContext _db;
        private readonly BtcPayService _btcPayService;
        private readonly LinkGenerator _linkGenerator;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BTCPayPaymentProvider(
            ICommonServices services,
            ILocalizationService localizationService,
            ICustomerService customerService,
            ISettingFactory settingFactory,
            ICurrencyService currencyService,
            SmartDbContext db,
            BtcPayService btcPayService,
            LinkGenerator linkGenerator,
            IHttpContextAccessor httpContextAccessor)
        {
            _localizationService = localizationService;
            _services = services;
            _currencyService = currencyService;
            _customerService = customerService;
            _settingFactory = settingFactory;
            _db = db;
            _btcPayService = btcPayService;
            _linkGenerator = linkGenerator;
            _httpContextAccessor = httpContextAccessor;
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public static string SystemName => "Smartstore.BTCPayServer";

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
		public override bool SupportVoid => true;

        /// <summary>
        /// Must be true when the payment method requests payment data during checkout.
        /// </summary>
        public override bool RequiresInteraction => false;

        /// <summary>
        /// For more information about supported payment types and there function make a right click on PaymentMethodType and go to definition.
        /// </summary>
        public override PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;


        public RouteInfo GetConfigurationRoute()
            => new(nameof(BtcPayAdminController.Configure), "BTCPayServer", new { area = "Admin" });


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

                string? sEmail;
                string? sFullName;
                Customer? myCustomer = await _customerService.GetAuthenticatedCustomerAsync();
                if (myCustomer == null)
                {
                    myCustomer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == processPaymentRequest.CustomerId) 
                                        ?? throw new Exception("Customer not found");
                    sEmail = myCustomer.BillingAddress?.Email;
                    sFullName = myCustomer.BillingAddress?.GetFullName(); 
                } else
                {
                    sEmail = myCustomer.Email;
                    sFullName = myCustomer.FullName;
                }
                
                var invoice =  await _btcPayService.CreateInvoice(settings, new PaymentDataModel()
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
                                        Lang = _services.WorkContext.WorkingLanguage.LanguageCulture,
                                        OrderUrl = new Uri(new Uri(myStore.Url), _linkGenerator.GetPathByAction("Details", "Order", new {orderId = processPaymentRequest.OrderGuid.ToString()})).ToString()
                                        
                                    }) ;
                
                result.AuthorizationTransactionResult = invoice.CheckoutLink;
                result.AuthorizationTransactionId = invoice.Id;
                result.CaptureTransactionResult =
                    invoice.Receipt?.Enabled is true ? invoice.CheckoutLink + "/receipt" : null;

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                throw new PaymentException(_localizationService.GetResource("Plugins.SmartStore.BTCPayServer.PaymentError"));
            }
            return await Task.FromResult(result);
        }

        /// <summary>
        /// Will be called after payment is processed
        /// </summary>
        public override async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            if (postProcessPaymentRequest.Order.PaymentStatus == PaymentStatus.Pending)
            {
                // Specify redirection URL here if your provider is of type PaymentMethodType.Redirection.
                // Core redirects to this URL automatically.
                postProcessPaymentRequest.RedirectUrl = postProcessPaymentRequest.Order.AuthorizationTransactionResult;

                if (_httpContextAccessor.HttpContext is null)
                    return;
                
                    var settings = _settingFactory.LoadSettings<BtcPaySettings>(postProcessPaymentRequest.Order.StoreId);
                    
                    
                    if (await _btcPayService.UpdateOrderWithInvoice(settings, postProcessPaymentRequest.Order,
                            postProcessPaymentRequest.Order.AuthorizationTransactionId))
                    {
                        var entry = _db.Update(postProcessPaymentRequest.Order);
                        await _db.SaveChangesAsync();
                        if(postProcessPaymentRequest.Order.PaymentStatus != PaymentStatus.Pending)
                        {

                            postProcessPaymentRequest.RedirectUrl =  _linkGenerator.GetUriByAction(_httpContextAccessor.HttpContext, "Details", "Order",
                                new {orderId = postProcessPaymentRequest.Order.Id});
                            
                        }
                    }
            }
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
        public override async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
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
              
                var sUrl = await _btcPayService.CreateRefund(settings, refundPaymentRequest);
                refundPaymentRequest.Order.AddOrderNote(_localizationService.GetResource("Plugins.SmartStore.BTCPayServer.NoteRefund") + sUrl, true);
                result.NewPaymentStatus = refundPaymentRequest.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                throw new PaymentException(_localizationService.GetResource("Plugins.SmartStore.BTCPayServer.PaymentError"));
            }

            return result;
        }

        public override async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest request)
        {
            try
            {
                var myStore = _services.StoreContext.CurrentStore;
                var settings = _settingFactory.LoadSettings<BtcPaySettings>(myStore.Id);
                var client = _btcPayService.GetClient(settings);
                var invoice =await client.MarkInvoiceStatus(settings.BtcPayStoreID,
                    request.Order.AuthorizationTransactionId,
                    new MarkInvoiceStatusRequest() {Status = InvoiceStatus.Invalid});

                return new VoidPaymentResult()
                {
                    NewPaymentStatus = invoice.Status == InvoiceStatus.Invalid
                        ? PaymentStatus.Voided
                        : request.Order.PaymentStatus
                };
            
            }
            catch (Exception e)
            {
                Logger.LogError(e, e.Message);
                return new VoidPaymentResult() {NewPaymentStatus = request.Order.PaymentStatus};
            }
        }
    }
}