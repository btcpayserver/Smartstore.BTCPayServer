
namespace Smartstore.BtcPay.Models
{
    [LocalizedDisplay("Plugins.Smartstore.BtcPay.")]
    public class ConfigurationModel : ModelBase
    {

        [LocalizedDisplay("*BtcPayUrl")]
        //[Url]
        [Required]
        public string BtcPayUrl { get; set; }

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

    }

}