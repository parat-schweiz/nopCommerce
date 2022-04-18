using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Payments.Quaestur.Services;
using Nop.Plugin.Payments.Quaestur.Components;
using Nop.Services.Logging;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Tax;

namespace Nop.Plugin.Payments.Quaestur
{
    /// <summary>
    /// Quaestur payment processor
    /// </summary>
    public class QuaesturPaymentProcessor : BasePlugin, IPaymentMethod
    {
		#region Fields

		private readonly CurrencySettings _currencySettings;
		private readonly IAddressService _addressService;
		private readonly ICountryService _countryService;
		private readonly ICurrencyService _currencyService;
		private readonly ICustomerService _customerService;
		private readonly IGenericAttributeService _genericAttributeService;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ILocalizationService _localizationService;
		private readonly IOrderService _orderService;
		private readonly IOrderTotalCalculationService _orderTotalCalculationService;
		private readonly IProductService _productService;
		private readonly ISettingService _settingService;
		private readonly IStateProvinceService _stateProvinceService;
		private readonly ITaxService _taxService;
		private readonly IWebHelper _webHelper;
		private readonly ILogger _logger;
		private readonly QuaesturHttpClient _quaesturHttpClient;
		private readonly QuaesturPaymentSettings _quaesturPaymentSettings;

		#endregion

		#region Ctor

		public QuaesturPaymentProcessor(CurrencySettings currencySettings,
			IAddressService addressService,
			ICountryService countryService,
			ICurrencyService currencyService,
			ICustomerService customerService,
			IGenericAttributeService genericAttributeService,
			IHttpContextAccessor httpContextAccessor,
			ILocalizationService localizationService,
			IOrderService orderService,
			IOrderTotalCalculationService orderTotalCalculationService,
			IProductService productService,
			ISettingService settingService,
			IStateProvinceService stateProvinceService,
			ITaxService taxService,
			IWebHelper webHelper,
			ILogger logger,
			QuaesturHttpClient quaesturHttpClient,
			QuaesturPaymentSettings quaesturPaymentSettings)
		{
			_currencySettings = currencySettings;
			_addressService = addressService;
			_countryService = countryService;
			_currencyService = currencyService;
			_customerService = customerService;
			_genericAttributeService = genericAttributeService;
			_httpContextAccessor = httpContextAccessor;
			_localizationService = localizationService;
			_orderService = orderService;
			_orderTotalCalculationService = orderTotalCalculationService;
			_productService = productService;
			_settingService = settingService;
			_stateProvinceService = stateProvinceService;
			_taxService = taxService;
			_webHelper = webHelper;
			_logger = logger;
			_quaesturHttpClient = quaesturHttpClient;
			_quaesturPaymentSettings = quaesturPaymentSettings;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Process a payment
		/// </summary>
		/// <param name="processPaymentRequest">Payment info required for an order processing</param>
		/// <returns>
		/// A task that represents the asynchronous operation
		/// The task result contains the process payment result
		/// </returns>
		public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
		{
			return Task.FromResult(new ProcessPaymentResult());
		}

		/// <summary>
		/// Post process payment (used by payment gateways that require redirecting to a third-party URL)
		/// </summary>
		/// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
		/// <returns>A task that represents the asynchronous operation</returns>
		public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
		{
			var amount = postProcessPaymentRequest.Order.OrderTotal;
			var reason = string.Format(
				"Order No {0}",
				postProcessPaymentRequest.Order.CustomOrderNumber);
			var url = string.Format(
				"{0}/orderdetails/{1}",
				_webHelper.GetStoreLocation(),
				postProcessPaymentRequest.Order.CustomOrderNumber);

			try
			{
				var transactionId = await _quaesturHttpClient.PrepareTransaction(
					postProcessPaymentRequest.Order.OrderGuid, amount, reason, url);

				await _genericAttributeService.SaveAttributeAsync<string>(postProcessPaymentRequest.Order, QuaesturHelper.OrderAttributeQuaesturTransactionId, transactionId);

				var quaesturUrl = string.Format(
					"{0}/payments/show/{1}",
					_quaesturPaymentSettings.ApiUrl,
					transactionId);
				_httpContextAccessor.HttpContext.Response.Redirect(quaesturUrl);
			}
			catch (ApiException exception)
			{
				var errorString = string.Format(
					"Faild to create Quaestur transaction for order {0}. {1}",
					postProcessPaymentRequest.Order.Id,
					exception.Message);

				await _logger.ErrorAsync(errorString);
				await _orderService.InsertOrderNoteAsync(new OrderNote
				{
					OrderId = postProcessPaymentRequest.Order.Id,
					Note = errorString,
					DisplayToCustomer = false,
					CreatedOnUtc = DateTime.UtcNow
				});
			}
		}

		/// <summary>
		/// Returns a value indicating whether payment method should be hidden during checkout
		/// </summary>
		/// <param name="cart">Shopping cart</param>
		/// <returns>
		/// A task that represents the asynchronous operation
		/// The task result contains the rue - hide; false - display.
		/// </returns>
		public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
		{
			return Task.FromResult(false);
		}

		/// <summary>
		/// Gets additional handling fee
		/// </summary>
		/// <param name="cart">Shopping cart</param>
		/// <returns>
		/// A task that represents the asynchronous operation
		/// The task result contains the additional handling fee
		/// </returns>
		public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
		{
			return Task.FromResult(0M);
		}

		/// <summary>
		/// Captures payment
		/// </summary>
		/// <param name="capturePaymentRequest">Capture payment request</param>
		/// <returns>
		/// A task that represents the asynchronous operation
		/// The task result contains the capture payment result
		/// </returns>
		public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
		{
			return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
		}

		/// <summary>
		/// Refunds a payment
		/// </summary>
		/// <param name="refundPaymentRequest">Request</param>
		/// <returns>
		/// A task that represents the asynchronous operation
		/// The task result contains the result
		/// </returns>
		public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
		{
			return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });
		}

		/// <summary>
		/// Voids a payment
		/// </summary>
		/// <param name="voidPaymentRequest">Request</param>
		/// <returns>
		/// A task that represents the asynchronous operation
		/// The task result contains the result
		/// </returns>
		public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
		{
			return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
		}

		/// <summary>
		/// Process recurring payment
		/// </summary>
		/// <param name="processPaymentRequest">Payment info required for an order processing</param>
		/// <returns>
		/// A task that represents the asynchronous operation
		/// The task result contains the process payment result
		/// </returns>
		public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
		{
			return Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });
		}

		/// <summary>
		/// Cancels a recurring payment
		/// </summary>
		/// <param name="cancelPaymentRequest">Request</param>
		/// <returns>
		/// A task that represents the asynchronous operation
		/// The task result contains the result
		/// </returns>
		public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
		{
			return Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });
		}

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the list of validating errors
        /// </returns>
        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult<IList<string>>(new List<string>());
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the payment info holder
        /// </returns>
        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentQuaestur/Configure";
        }
        
        /// <summary>
        /// Gets a type of a view component for displaying plugin in public store
        /// </summary>
        /// <returns>View component type</returns>
        public Type GetPublicViewComponent()
        {
            return typeof(PaymentQuaesturViewComponent);
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentQuaestur";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new QuaesturPaymentSettings());

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.Quaestur.Fields.RedirectionTip"] = "You will be redirected to Quaestur site to complete the order.",
                ["Plugins.Payments.Quaestur.Fields.ApiUrl"] = "API URL",
                ["Plugins.Payments.Quaestur.Fields.ApiUrl.Hint"] = "Specify the URL of the Quaestur API.",
                ["Plugins.Payments.Quaestur.Fields.ApiClientId"] = "API Client ID",
                ["Plugins.Payments.Quaestur.Fields.ApiClientId.Hint"] = "Specify the client ID for the API.",
                ["Plugins.Payments.Quaestur.Fields.ApiClientSecret"] = "API Client Secret",
                ["Plugins.Payments.Quaestur.Fields.ApiClientSecret.Hint"] = "Specify the client secret for the API",
                ["Plugins.Payments.Quaestur.Instructions"] = @"Configure the Quaestur API.",
                ["Plugins.Payments.Quaestur.PaymentMethodDescription"] = "You will be redirected to Quaestur site to complete the payment"
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
            await _settingService.DeleteSettingAsync<QuaesturPaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Quaestur");

            await base.UninstallAsync();
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.Quaestur.PaymentMethodDescription");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => false;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => false;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => false;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => false;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        #endregion
    }
}
