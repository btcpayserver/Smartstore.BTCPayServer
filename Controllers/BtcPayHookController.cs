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
                try
                {
                    OrderGuid = jsonData.metadata.orderId ?? string.Empty;
                    BtcPayEvent = jsonData.type ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message);
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }

                if (String.IsNullOrEmpty(OrderGuid) || string.IsNullOrEmpty(BtcPayEvent))
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

                switch (BtcPayEvent)
                {
                    case "InvoiceReceivedPayment":
                    case "InvoiceSettled":
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.AuthorizationTransactionId = jsonData.invoiceId;
                        break;
                    case "InvoiceExpired":
                    case "InvoiceInvalid":
                        if (order.OrderStatus != OrderStatus.Complete)
                            order.PaymentStatus = PaymentStatus.Voided;
                        break;
                }
                order.HasNewPaymentNotification = true;
                order.AddOrderNote($"BTCPay Event: {BtcPayEvent} - PaymentStatus: {order.PaymentStatus.ToString()}", true);

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
