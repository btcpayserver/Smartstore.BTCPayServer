using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Smartstore.BTCPay.Models
{
    public struct BtcPayPullPaymentModel
    {
        public string id;
        public string name;
        public string description;
        public string amount;
        public string currency;
        public uint period;
        public string[] paymentMethods;
    }
}
