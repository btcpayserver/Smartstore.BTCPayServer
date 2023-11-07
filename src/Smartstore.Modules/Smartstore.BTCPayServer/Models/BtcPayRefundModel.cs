namespace Smartstore.BTCPayServer.Models
{
    public record BtcPayRefundModel
    {
        public string name;
        public string description;
        public string paymentMethod;
        public string refundVariant;
    }

    public record BtcPayRefundCustomModel : BtcPayRefundModel
    {
        public decimal customAmount;
        public string customCurrency;
    }
}
