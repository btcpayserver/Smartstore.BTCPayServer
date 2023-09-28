global using System;
global using System.ComponentModel.DataAnnotations;
global using System.Linq;
global using System.Threading.Tasks;
global using FluentValidation;
global using Smartstore.Core.Localization;
global using Smartstore.Web.Modelling;
using Smartstore.Engine.Modularity;
using Smartstore.BtcPay.Settings;

namespace Smartstore.BtcPay
{
    internal class Module : ModuleBase
    {
        public override async Task InstallAsync(ModuleInstallationContext context)
        {
            await SaveSettingsAsync<BtcPaySettings>(new BtcPaySettings
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
            await DeleteLanguageResourcesAsync("Plugins.Payment.BtcPay");

            await base.UninstallAsync();
        }
    }
}
