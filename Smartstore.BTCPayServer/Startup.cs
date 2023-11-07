using Microsoft.Extensions.DependencyInjection;
using Smartstore.BTCPayServer.Services;
using Smartstore.Engine;
using Smartstore.Engine.Builders;

namespace Smartstore.BTCPayServer
{
    internal class Startup : StarterBase
    {
        public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
        {
            services.AddSingleton<BtcPayService>();
        }
    }
}