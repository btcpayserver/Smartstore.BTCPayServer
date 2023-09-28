using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Smartstore.BtcPay.Models
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
