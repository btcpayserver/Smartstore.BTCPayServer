using Smartstore.Core.Configuration;

namespace Smartstore.BTCPayServer.Configuration
{
    /// <summary>
    /// Represents settings of BtcPay payment plugin
    /// </summary>
    public class BtcPaySettings : ISettings
    {
        /// <summary>
        /// The url of your BTCPay instance
        /// </summary>
        public string BtcPayUrl { get; set; }

        /// <summary>
        /// The API Key value generated in your BTCPay instance
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// The BTCPay StoreID
        /// </summary>
        public string? BtcPayStoreID { get; set; }

        /// <summary>
        /// The WebHook Secret value generated in your BTCPay instance
        /// </summary>
        public string? WebHookSecret { get; set; }

        public decimal AdditionalFee { get; set; }

        public bool AdditionalFeePercentage { get; set; }

    }
}