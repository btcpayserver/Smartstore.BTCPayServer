
using System.ComponentModel.DataAnnotations;
using Smartstore.Web.Modelling;

namespace Smartstore.BTCPayServer.Models
{
    [ LocalizedDisplay("Plugins.SmartStore.BTCPayServer.")]
    public class ConfigurationModel : ModelBase
    {

        [LocalizedDisplay("*BtcPayUrl")]
        //[Url]
        [Required]
        public string? BtcPayUrl { get; set; }

        [LocalizedDisplay("*ApiKey")]
        public string? ApiKey { get; set; }

        [LocalizedDisplay("*BtcPayStoreID")]
        public string? BtcPayStoreID { get; set; }

        [LocalizedDisplay("*WebHookSecret")]
        public string? WebHookSecret { get; set; }

        [LocalizedDisplay("Admin.Configuration.Payment.Methods.AdditionalFee")]
        public decimal AdditionalFee { get; set; }

        [LocalizedDisplay("Admin.Configuration.Payment.Methods.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }

        public bool IsConfigured()
        {
            return
                !string.IsNullOrEmpty(ApiKey) &&
                !string.IsNullOrEmpty(BtcPayStoreID) &&
                !string.IsNullOrEmpty(BtcPayUrl) && 
                !string.IsNullOrEmpty(WebHookSecret);
        }

    }

}