using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Smartstore.BtcPay.Models;
using Smartstore.BtcPay.Settings;
using Smartstore.Core.Checkout.Payment;

namespace Smartstore.BtcPay.Services
{
    public class BtcPayService
    {

        public static bool CheckSecretKey(string key, string message, string signature)
        {
            var msgBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message));
            string hashString = string.Empty;
            foreach (byte x in msgBytes)
            {
                hashString += String.Format("{0:x2}", x);
            }
            return (hashString == signature);
        }

        public string CreateInvoice(BtcPaySettings settings, PaymentDataModel paymentData)
        {

            try
            {
                var invoice = new BtcPayInvoiceModel()
                {
                    currency = paymentData.CurrencyCode,
                    amount = paymentData.Amount.ToString("#.##"),
                    checkout = new BtcPayInvoiceCheckout()
                    {
                        defaultLanguage = paymentData.Lang,
                        redirectURL = paymentData.RedirectionURL,
                        redirectAutomatically = true,
                        requiresRefundEmail = false
                    },
                    metadata = new BtcPayInvoiceMetaData()
                    {
                        buyerEmail = paymentData.BuyerEmail,
                        buyerName = paymentData.BuyerName,
                        orderId = paymentData.OrderID,
                        itemDesc = paymentData.Description
                    }
                };
                var invoiceJson = JsonConvert.SerializeObject(invoice, Formatting.None);

                string sUrl = settings.BtcPayUrl.EndsWith("/") ? settings.BtcPayUrl : settings.BtcPayUrl + "/";
                var client = new HttpClient()
                {
                    BaseAddress = new Uri($"{sUrl}api/v1/stores/{settings.BtcPayStoreID}/")
                };
                var webRequest = new HttpRequestMessage(HttpMethod.Post, "invoices")
                {
                    Content = new StringContent(invoiceJson, Encoding.UTF8, "application/json"),
                };
                webRequest.Headers.Add("Authorization", $"token {settings.ApiKey}");

                string sRep;
                using (var rep = client.SendAsync(webRequest).Result)
                {
                    rep.EnsureSuccessStatusCode();
                    using (var rdr = new StreamReader(rep.Content.ReadAsStream()))
                    {
                        sRep = rdr.ReadToEnd();
                    }
                }

                dynamic JsonRep = JsonConvert.DeserializeObject<dynamic>(sRep);
                return JsonRep.checkoutLink;

            }
            catch
            {
                throw;
            }

        }

        public string CreateRefund(BtcPaySettings settings, RefundPaymentRequest refundRequest)
        {
            try
            {
                BtcPayRefundModel refund;
                if (refundRequest.IsPartialRefund)
                {
                    refund = new BtcPayRefundCustomModel()
                    {
                        name = "Refund order " + refundRequest.Order.OrderGuid,
                        description = "Partial",
                        paymentMethod = "BTC",
                        refundVariant = "Custom",
                        customAmount = refundRequest.AmountToRefund.Amount,
                        customCurrency = refundRequest.Order.CustomerCurrencyCode
                    };
                }else
                {
                    refund = new BtcPayRefundModel()
                    {
                        name = "Refund order " + refundRequest.Order.OrderGuid,
                        description = "Full",
                        paymentMethod = "BTC",
                        refundVariant = "RateThen"
                    };
                };

                var refundJson = JsonConvert.SerializeObject(refund, Formatting.None);
                var sBtcPayInvoiceID = refundRequest.Order.AuthorizationTransactionId;

                string sUrl = settings.BtcPayUrl.EndsWith("/") ? settings.BtcPayUrl : settings.BtcPayUrl + "/";
                var client = new HttpClient()
                {
                    BaseAddress = new Uri($"{sUrl}api/v1/stores/{settings.BtcPayStoreID}/invoices/{sBtcPayInvoiceID}/")
                };
                var webRequest = new HttpRequestMessage(HttpMethod.Post, "refund")
                {
                    Content = new StringContent(refundJson, Encoding.UTF8, "application/json"),
                };
                webRequest.Headers.Add("Authorization", $"token {settings.ApiKey}");

                string sRep;
                using (var rep = client.SendAsync(webRequest).Result)
                {
                    rep.EnsureSuccessStatusCode();
                    using (var rdr = new StreamReader(rep.Content.ReadAsStream()))
                    {
                        sRep = rdr.ReadToEnd();
                    }
                }

                dynamic JsonRep = JsonConvert.DeserializeObject<dynamic>(sRep);

                return JsonRep.viewLink;
            }
            catch
            {

                throw;
            }
        }
    }
}