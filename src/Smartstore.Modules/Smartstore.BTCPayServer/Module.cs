using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Smartstore.BTCPayServer.Configuration;
using Smartstore.Core.Common;
using Smartstore.Core.Data;
using Smartstore.Engine.Modularity;
using Smartstore.Http;

namespace Smartstore.BTCPayServer
{
    internal class Module : ModuleBase, IConfigurable
    {
        private readonly SmartDbContext _smartDbContext;

        public Module(SmartDbContext smartDbContext)
        {
            _smartDbContext = smartDbContext;
        }

        public override async Task InstallAsync(ModuleInstallationContext context)
        {
            await SaveSettingsAsync(new BtcPaySettings
            {
                BtcPayUrl = "", ApiKey = "", BtcPayStoreID = "", WebHookSecret = ""
            });
            await ImportLanguageResourcesAsync();

            await AddBTCCurrency(_smartDbContext);
            await base.InstallAsync(context);
        }

        private async Task AddBTCCurrency(SmartDbContext smartDbContext)
        {
            try
            {
                await smartDbContext.Currencies.AddAsync(new Currency
                {
                    DisplayLocale = "en-US",
                    Name = "Bitcoin",
                    CurrencyCode = "BTC",
                    CustomFormatting = "{0} ₿",
                    Published = true,
                    RoundNumDecimals = 8,
                    
                    DisplayOrder = 1,
                });
                await smartDbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        public override async Task UninstallAsync()
        {
            await DeleteSettingsAsync<BtcPaySettings>();

            await DeleteLanguageResourcesAsync();
            await DeleteLanguageResourcesAsync("Plugins.Payment.BTCPayServer");

            await base.UninstallAsync();
        }


        public RouteInfo GetConfigurationRoute()
            => new("Configure", "BtcPayAdmin", new {area = "Admin"});
    }
}