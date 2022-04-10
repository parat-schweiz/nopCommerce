using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Nop.Core.Infrastructure;
using Nop.Services.Authentication.External;

namespace Nop.Plugin.ExternalAuth.OpenIdConnect.Infrastructure
{
    /// <summary>
    /// Represents registrar of OpenIdConnect authentication service
    /// </summary>
    public class OpenIdConnectAuthenticationRegistrar : IExternalAuthenticationRegistrar
    {
        /// <summary>
        /// Configure
        /// </summary>
        /// <param name="builder">Authentication builder</param>
        public void Configure(AuthenticationBuilder builder)
        {
            builder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                //set credentials
                var settings = EngineContext.Current.Resolve<OpenIdConnectExternalAuthSettings>();
                options.ClientId = settings.ClientKeyIdentifier;
                options.ClientSecret = settings.ClientSecret;
                options.Authority = settings.AuthorityUrl;
                options.DisableTelemetry = true;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.CallbackPath = new PathString(OpenIdConnectAuthenticationDefaults.SigninCallbackPath);
                options.GetClaimsFromUserInfoEndpoint = true;
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "firstname");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "fullname");
                options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "lastname");
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");

                //store access and refresh tokens for the further usage
                options.SaveTokens = true;

                //set custom events handlers
                options.Events = new OpenIdConnectEvents
                {
                    //in case of error, redirect the user to the specified URL
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();

                        var errorUrl = context.Properties.GetString(OpenIdConnectAuthenticationDefaults.ErrorCallback);
                        context.Response.Redirect(errorUrl);

                        return Task.FromResult(0);
                    },
                    OnUserInformationReceived = n =>
                    {
                        return Task.FromResult(0);
                    }
                };
            });
        }
    }
}
