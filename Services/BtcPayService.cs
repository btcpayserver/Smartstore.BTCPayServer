using System;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;
using Smartstore.BtcPay.Models;
using Smartstore.BtcPay.Settings;
using Smartstore.BTCPay.Models;
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

            var sRep = string.Empty;
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

                using (var rep = client.SendAsync(webRequest).Result)
                {
                    try
                    {
                        using (var rdr = new StreamReader(rep.Content.ReadAsStream()))
                        {
                            sRep = rdr.ReadToEnd();
                        }
                    }
                    catch { }
                    rep.EnsureSuccessStatusCode();
                }

                dynamic JsonRep = JsonConvert.DeserializeObject<dynamic>(sRep);
                return JsonRep.checkoutLink;

            }
            catch (HttpRequestException ex)
            {
                var sMsg = $"HTTP Error {ex.StatusCode.Value}";
                dynamic JsonRep;
                try { 
                    JsonRep = JsonConvert.DeserializeObject<dynamic>(sRep);
                    sMsg += $" - {JsonRep.message}";
                } catch { }

                throw new Exception (sMsg);
            }
            catch
            {
                throw;
            }
        }

        public string CreateRefund(BtcPaySettings settings, RefundPaymentRequest refundRequest)
        {
            var sRep = string.Empty;
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
                } else
                {
                    refund = new BtcPayRefundModel()
                    {
                        name = "Refund order " + refundRequest.Order.OrderGuid,
                        description = "Full",
                        paymentMethod = "BTC",
                        refundVariant = "Fiat"
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

                using (var rep = client.SendAsync(webRequest).Result)
                {
                    try {
                        using (var rdr = new StreamReader(rep.Content.ReadAsStream()))
                        {
                            sRep = rdr.ReadToEnd();
                        }
                    } catch { }
                    
                    rep.EnsureSuccessStatusCode();
                }

                dynamic JsonRep = JsonConvert.DeserializeObject<dynamic>(sRep);

                return JsonRep.viewLink;
            }
            catch (HttpRequestException ex)
            {
                var sMsg = $"HTTP Error {ex.StatusCode.Value}";
                dynamic JsonRep;
                try
                {
                    JsonRep = JsonConvert.DeserializeObject<dynamic>(sRep);
                    sMsg += $" - {JsonRep.message}";
                }
                catch { }

                throw new Exception(sMsg);
            }
            catch
            {
                throw;
            }

        }

        public string CreateWebHook(BtcPaySettings settings, string WebHookUrl)
        {
            var sRep = string.Empty;

            try { 
                BtcPayHookModel hook = new BtcPayHookModel()
                {
                    url = WebHookUrl
                };
                var hookJson = JsonConvert.SerializeObject(hook, Formatting.None);

                string sUrl = settings.BtcPayUrl.EndsWith("/") ? settings.BtcPayUrl : settings.BtcPayUrl + "/";
                var client = new HttpClient()
                {
                    BaseAddress = new Uri($"{sUrl}api/v1/stores/{settings.BtcPayStoreID}/")
                };
                var webRequest = new HttpRequestMessage(HttpMethod.Post, "webhooks")
                {
                    Content = new StringContent(hookJson, Encoding.UTF8, "application/json"),
                };
                webRequest.Headers.Add("Authorization", $"token {settings.ApiKey}");

                using (var rep = client.SendAsync(webRequest).Result)
                {
                    try
                    {
                        using (var rdr = new StreamReader(rep.Content.ReadAsStream()))
                        {
                            sRep = rdr.ReadToEnd();
                        }
                    }
                    catch { }

                    rep.EnsureSuccessStatusCode();
                }

                dynamic JsonRep = JsonConvert.DeserializeObject<dynamic>(sRep);

                return JsonRep.secret;
            }
            catch (HttpRequestException ex)
            {
                var sMsg = $"HTTP Error {ex.StatusCode.Value}";
                dynamic JsonRep;
                try
                {
                    JsonRep = JsonConvert.DeserializeObject<dynamic>(sRep);
                    sMsg += $" - {JsonRep.message}";
                }
                catch { }

                throw new Exception(sMsg);
            }
            catch
            {
                    throw;
            }
        }
    }
}