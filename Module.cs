using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Smartstore.BTCPayServer.Configuration;
using Smartstore.Engine.Modularity;
using Smartstore.Http;

namespace Smartstore.BTCPayServer
{
    internal class Module : ModuleBase, IConfigurable
    {
        public override async Task InstallAsync(ModuleInstallationContext context)
        {
            await SaveSettingsAsync(new BtcPaySettings
            {
                BtcPayUrl = "",
                ApiKey = "",
                BtcPayStoreID = "",
                WebHookSecret = ""
            });
            await ImportLanguageResourcesAsync();
            await base.InstallAsync(context);
        }

        public override async Task UninstallAsync()
        {
            await DeleteSettingsAsync<BtcPaySettings>();

            await DeleteLanguageResourcesAsync();
            await DeleteLanguageResourcesAsync("Plugins.Payment.BTCPayServer");

            await base.UninstallAsync();
        }


        public RouteInfo GetConfigurationRoute()
            => new("Configure", "BtcPayAdmin", new { area = "Admin" });
    }
}
