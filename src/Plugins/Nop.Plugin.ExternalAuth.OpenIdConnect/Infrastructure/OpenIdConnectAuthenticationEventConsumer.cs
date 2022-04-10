using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Nop.Core.Domain.Customers;
using Nop.Services.Authentication.External;
using Nop.Services.Common;
using Nop.Services.Events;
using Nop.Services.Customers;

namespace Nop.Plugin.ExternalAuth.OpenIdConnect.Infrastructure
{
    /// <summary>
    /// OpenIdConnect authentication event consumer (used for saving customer fields on registration)
    /// </summary>
    public partial class OpenIdConnectAuthenticationEventConsumer : IConsumer<CustomerAutoRegisteredByExternalMethodEvent>
    {
        #region Fields

        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;

        #endregion

        #region Ctor

        public OpenIdConnectAuthenticationEventConsumer(
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService)
        {
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(CustomerAutoRegisteredByExternalMethodEvent eventMessage)
        {
            if (eventMessage?.Customer == null || eventMessage.AuthenticationParameters == null)
                return;

            //handle event only for this authentication method
            if (!eventMessage.AuthenticationParameters.ProviderSystemName.Equals(OpenIdConnectAuthenticationDefaults.SystemName))
                return;

            var customer = eventMessage.Customer;
            //store some of the customer fields
            var firstName = eventMessage.AuthenticationParameters.Claims?.FirstOrDefault(claim => claim.Type == ClaimTypes.GivenName)?.Value;
            if (!string.IsNullOrEmpty(firstName))
                customer.FirstName = firstName;

            var lastName = eventMessage.AuthenticationParameters.Claims?.FirstOrDefault(claim => claim.Type == ClaimTypes.Surname)?.Value;
            if (!string.IsNullOrEmpty(lastName))
                customer.LastName = lastName;

            await _customerService.UpdateCustomerAsync(customer);

            //assign roles
            var automaticRole = await _customerService.GetCustomerRoleBySystemNameAsync("Member");
            var customerRoleIds = await _customerService.GetCustomerRoleIdsAsync(eventMessage.Customer);
            if (!customerRoleIds.Contains(automaticRole.Id))
            {
                await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping { CustomerRoleId = automaticRole.Id, CustomerId = eventMessage.Customer.Id });
            }
        }

        #endregion
    }
}
