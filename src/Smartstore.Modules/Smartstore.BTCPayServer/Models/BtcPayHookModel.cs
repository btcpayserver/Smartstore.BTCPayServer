

namespace Smartstore.BTCPayServer.Models
{
    public record BtcPayHookModel
    {
        public bool enabled = true;
        public bool automaticRedelivery = true;
        public string url;
        public BtcPayHookAuthorizedEvents authorizedEvents = new BtcPayHookAuthorizedEvents();
        public string secret;
    }

    public record BtcPayHookAuthorizedEvents
    {
        public bool everything = true;
    }
}
