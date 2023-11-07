
namespace Smartstore.BTCPayServer.Models
{
    public struct PaymentDataModel
    {
        public string CurrencyCode;
        public decimal Amount;
        public string OrderID;
        public int StoreID;
        public int CustomerID;
        public string Description;
        public string? BuyerEmail;
        public string? BuyerName;
        public string RedirectionURL;
        public string Lang;
        public string OrderUrl { get; set; }
    }
}