using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Smartstore.Core;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Data;
using Smartstore.BtcPay.Providers;
using Smartstore.Web.Controllers;
using Smartstore.BtcPay.Services;
using Smartstore.BtcPay.Settings;

namespace Smartstore.SwissBitcoinPay.Controllers
{
    public class BtcPayHookController : PublicController
    {
        private readonly ILogger _logger;
        private readonly SmartDbContext _db;
        private readonly BtcPaySettings _settings;

        public BtcPayHookController(IOrderService orderService,
            BtcPaySettings settings,
            SmartDbContext db,
           ICommonServices services,
            ILogger logger)
        {
            _logger = logger;
            _db = db;
            _settings = settings;
        }

        [HttpPost]
        public async Task<IActionResult> Process([FromHeader(Name = "BTCPAY-SIG")] string BtcPaySig)
        {
            try
            {
                string jsonStr = await new StreamReader(Request.Body).ReadToEndAsync();
                dynamic jsonData = JsonConvert.DeserializeObject(jsonStr);
                var BtcPaySecret = BtcPaySig.Split('=')[1];

                string OrderGuid;
                string BtcPayEvent;
                string InvoiceID;
                bool bAfterExpiration;

                try
                {
                    OrderGuid = jsonData.metadata.orderId ?? string.Empty;
                    BtcPayEvent = jsonData.type ?? string.Empty;
                    InvoiceID = jsonData.invoiceId ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }

                if (String.IsNullOrEmpty(OrderGuid) || string.IsNullOrEmpty(BtcPayEvent) || string.IsNullOrEmpty(InvoiceID))
                {
                    Logger.Error("Missing fields in request");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }

                if (!BtcPayService.CheckSecretKey(_settings.WebHookSecret, jsonStr, BtcPaySecret))
                {
                    Logger.Error("Bad secret key");
                    return StatusCode(StatusCodes.Status400BadRequest);
                }


                var order = await _db.Orders.FirstOrDefaultAsync(x =>
                    x.PaymentMethodSystemName == PaymentProvider.SystemName &&
                    x.OrderGuid == new Guid(OrderGuid));
                if (order == null)
                {
                    Logger.Error("Order not found");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }

                string sDesc = "";

                switch (BtcPayEvent)
                {
                    case "InvoiceReceivedPayment":
                        bool bAtferExpiration = (bool)jsonData.afterExpiration;
                        sDesc = ", Invoice (partial) payment incoming (unconfirmed)"
                                + (bAtferExpiration ? " after invoice was already expired." : ". Waiting for settlement.");
                        break;
                    case "InvoicePaymentSettled":
                        if (order.OrderStatus == OrderStatus.Cancelled)
                            sDesc = ", Invoice fully settled after invoice was already canceled. Needs manual checking.";

                        break;
                    case "InvoiceProcessing": // The invoice is paid in full.
                        bool bOverPaid = (bool)jsonData.overPaid;
                        sDesc = bOverPaid ? ", Invoice payment received fully with overpayment, waiting for settlement."
                                                  : "Invoice payment received fully, waiting for settlement.";
                        break;
                    case "InvoiceExpired":
                        bool bpartiallyPaid = (bool)jsonData.partiallyPaid;
                        if (bpartiallyPaid)
                            sDesc = ", Invoice expired but was paid partially, please check.";

                        if (order.OrderStatus != OrderStatus.Complete)
                        {
                            order.PaymentStatus = PaymentStatus.Voided;
                            order.OrderStatus = OrderStatus.Cancelled;
                        }
                        break;
                    case "InvoiceInvalid":
                        if (order.OrderStatus != OrderStatus.Complete)
                        {
                            order.PaymentStatus = PaymentStatus.Voided;
                            order.OrderStatus = OrderStatus.Cancelled;
                        }
                        break;
                    case "InvoiceSettled":
                        order.OrderStatus = OrderStatus.Processing;
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.AuthorizationTransactionId = jsonData.invoiceId;
                        break;
                }
                order.HasNewPaymentNotification = true;
                order.AddOrderNote($"BTCPay Invoice: {InvoiceID} - BTCPay Event: {BtcPayEvent} {sDesc} - PaymentStatus: {order.PaymentStatus.ToString()}", true);

                await _db.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return StatusCode(StatusCodes.Status400BadRequest);
            }
        }

    }
}
