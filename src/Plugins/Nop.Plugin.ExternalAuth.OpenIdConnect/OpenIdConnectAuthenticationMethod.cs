using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Plugin.ExternalAuth.OpenIdConnect.Components;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.ExternalAuth.OpenIdConnect
{
    /// <summary>
    /// Represents method for the authentication with OpenIdConnect account
    /// </summary>
    public class OpenIdConnectAuthenticationMethod : BasePlugin, IExternalAuthenticationMethod
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public OpenIdConnectAuthenticationMethod(ILocalizationService localizationService,
            ISettingService settingService,
            IWebHelper webHelper)
        {
            _localizationService = localizationService;
            _settingService = settingService;
            _webHelper = webHelper;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/OpenIdConnectAuthentication/Configure";
        }

        /// <summary>
        /// Gets a type of a view component for displaying plugin in public store
        /// </summary>
        /// <returns>View component type</returns>
        public Type GetPublicViewComponent()
        {
            return typeof(OpenIdConnectAuthenticationViewComponent);
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return OpenIdConnectAuthenticationDefaults.VIEW_COMPONENT_NAME;
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new OpenIdConnectExternalAuthSettings());

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.ExternalAuth.OpenIdConnect.ClientKeyIdentifier"] = "App ID/API Key",
                ["Plugins.ExternalAuth.OpenIdConnect.ClientKeyIdentifier.Hint"] = "Enter your app ID/API key here.",
                ["Plugins.ExternalAuth.OpenIdConnect.ClientSecret"] = "App Secret",
                ["Plugins.ExternalAuth.OpenIdConnect.ClientSecret.Hint"] = "Enter your app secret here.",
                ["Plugins.ExternalAuth.OpenIdConnect.AuthorityUrl"] = "Authority URL",
                ["Plugins.ExternalAuth.OpenIdConnect.AuthorityUrl.Hint"] = "Enter your authority url here.",
                ["Plugins.ExternalAuth.OpenIdConnect.Instructions"] = "Enter the config."
            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<OpenIdConnectExternalAuthSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.ExternalAuth.OpenIdConnect");

            await base.UninstallAsync();
        }

        #endregion
    }
}
