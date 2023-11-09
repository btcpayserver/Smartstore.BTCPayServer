using BTCPayServer.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Smartstore.BTCPayServer.Configuration;
using Smartstore.BTCPayServer.Providers;
using Smartstore.BTCPayServer.Services;
using Smartstore.Core;
using Smartstore.Core.Data;
using Smartstore.Web.Controllers;

namespace Smartstore.BTCPayServer.Controllers
{
    
    public class BtcPayHookController : PublicController
    {
        private readonly BtcPayService _btcPayService;
        private readonly SmartDbContext _db;
        private readonly ICommonServices _services;

        public BtcPayHookController(
            SmartDbContext db,
            ICommonServices services,
            BtcPayService btcPayService)
        {
            _btcPayService = btcPayService;
            _db = db;
            _services = services;
        }



        [HttpPost][AllowAnonymous]
        public async Task<IActionResult> Process([FromHeader(Name = "BTCPAY-SIG")] string btcPaySig)
        {
            try
            {
                string jsonStr = await new StreamReader(Request.Body).ReadToEndAsync();
                var webhookEvent = JsonConvert.DeserializeObject<WebhookInvoiceEvent>(jsonStr);
                var signature = btcPaySig.Split('=')[1];
                if(webhookEvent is null ||  webhookEvent?.InvoiceId?.StartsWith("__test__") is true || webhookEvent?.Type == WebhookEventType.InvoiceCreated)
                {
                   return Ok();
                }

                if ( webhookEvent?.InvoiceId is null || webhookEvent.Metadata?.TryGetValue("orderId", out var orderIdToken) is not true || orderIdToken.ToString() is not {} orderId)
                {
                    Logger.Error("Missing fields in request");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }
                
                var order = await _db.Orders.FirstOrDefaultAsync(x =>
                    x.PaymentMethodSystemName == BTCPayPaymentProvider.SystemName &&
                    x.OrderGuid == new Guid(orderId));
                
                if (order is null)
                {
                    Logger.Error("Order not found");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }
                var settings = await  _services.SettingFactory.LoadSettingsAsync<BtcPaySettings>(order.StoreId);
              
                if (settings.WebHookSecret is not  null && !BtcPayService.CheckSecretKey(settings.WebHookSecret, jsonStr, signature))
                {
                    Logger.Error("Bad secret key");
                    return StatusCode(StatusCodes.Status400BadRequest);
                }
                var invoice = await  _btcPayService.GetInvoice(settings, webhookEvent.InvoiceId);

                if (await _btcPayService.UpdateOrderWithInvoice(order, invoice, webhookEvent))
                {
                    await _db.SaveChangesAsync();
                }
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
