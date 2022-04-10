using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nop.Core;
using Nop.Plugin.ExternalAuth.OpenIdConnect.Models;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.OpenIdConnect.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class OpenIdConnectAuthenticationController : BasePluginController
    {
        #region Fields

        private readonly OpenIdConnectExternalAuthSettings _openIdConnectExternalAuthSettings;
        private readonly IAuthenticationPluginManager _authenticationPluginManager;
        private readonly IExternalAuthenticationService _externalAuthenticationService;
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IOptionsMonitorCache<OpenIdConnectOptions> _optionsCache;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;

        #endregion

        #region Ctor

        public OpenIdConnectAuthenticationController(OpenIdConnectExternalAuthSettings openIdConnectExternalAuthSettings,
            IAuthenticationPluginManager authenticationPluginManager,
            IExternalAuthenticationService externalAuthenticationService,
            ILocalizationService localizationService,
            INotificationService notificationService,
            IOptionsMonitorCache<OpenIdConnectOptions> optionsCache,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWorkContext workContext)
        {
            _openIdConnectExternalAuthSettings = openIdConnectExternalAuthSettings;
            _authenticationPluginManager = authenticationPluginManager;
            _externalAuthenticationService = externalAuthenticationService;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _optionsCache = optionsCache;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _workContext = workContext;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageExternalAuthenticationMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                ClientId = _openIdConnectExternalAuthSettings.ClientKeyIdentifier,
                ClientSecret = _openIdConnectExternalAuthSettings.ClientSecret,
                AuthorityUrl = _openIdConnectExternalAuthSettings.AuthorityUrl
            };

            return View("~/Plugins/ExternalAuth.OpenIdConnect/Views/Configure.cshtml", model);
        }

        [HttpPost]        
        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageExternalAuthenticationMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //save settings
            _openIdConnectExternalAuthSettings.ClientKeyIdentifier = model.ClientId;
            _openIdConnectExternalAuthSettings.ClientSecret = model.ClientSecret;
            _openIdConnectExternalAuthSettings.AuthorityUrl = model.AuthorityUrl;
            await _settingService.SaveSettingAsync(_openIdConnectExternalAuthSettings);

            //clear OpenIdConnect authentication options cache
            _optionsCache.TryRemove(OpenIdConnectDefaults.AuthenticationScheme);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        public async Task<IActionResult> Login(string returnUrl)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var methodIsAvailable = await _authenticationPluginManager
                .IsPluginActiveAsync(OpenIdConnectAuthenticationDefaults.SystemName, await _workContext.GetCurrentCustomerAsync(), store.Id);
            if (!methodIsAvailable)
                throw new NopException("OpenIdConnect authentication module cannot be loaded");

            if (string.IsNullOrEmpty(_openIdConnectExternalAuthSettings.ClientKeyIdentifier) ||
                string.IsNullOrEmpty(_openIdConnectExternalAuthSettings.ClientSecret) ||
                string.IsNullOrEmpty(_openIdConnectExternalAuthSettings.AuthorityUrl))
            {
                throw new NopException("OpenIdConnect authentication module not configured");
            }

            //configure login callback action
            var authenticationProperties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("LoginCallback", "OpenIdConnectAuthentication", new { returnUrl = returnUrl })
            };
            authenticationProperties.SetString(OpenIdConnectAuthenticationDefaults.ErrorCallback, Url.RouteUrl("Login", new { returnUrl }));

            return Challenge(authenticationProperties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> LoginCallback(string returnUrl)
        {
            //authenticate OpenIdConnect user
            var authenticateResult = await HttpContext.AuthenticateAsync(OpenIdConnectDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded || !authenticateResult.Principal.Claims.Any())
                return RedirectToRoute("Login");

            //create external authentication parameters
            var authenticationParameters = new ExternalAuthenticationParameters
            {
                ProviderSystemName = OpenIdConnectAuthenticationDefaults.SystemName,
                AccessToken = await HttpContext.GetTokenAsync(OpenIdConnectDefaults.AuthenticationScheme, "access_token"),
                Email = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.Email)?.Value,
                ExternalIdentifier = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value,
                ExternalDisplayIdentifier = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.Name)?.Value,
                Claims = authenticateResult.Principal.Claims.Select(claim => new ExternalAuthenticationClaim(claim.Type, claim.Value)).ToList()
            };

            //authenticate Nop user
            return await _externalAuthenticationService.AuthenticateAsync(authenticationParameters, returnUrl);
        }

        #endregion
    }
}
