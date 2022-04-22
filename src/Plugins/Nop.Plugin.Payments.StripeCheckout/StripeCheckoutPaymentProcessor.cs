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
using Nop.Plugin.Payments.StripeCheckout.Services;
using Nop.Plugin.Payments.StripeCheckout.Components;
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

namespace Nop.Plugin.Payments.StripeCheckout
{
    /// <summary>
    /// StripeCheckout payment processor
    /// </summary>
    public class StripeCheckoutPaymentProcessor : BasePlugin, IPaymentMethod
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
		private readonly StripeCheckoutHttpClient _stripeCheckoutHttpClient;
		private readonly StripeCheckoutPaymentSettings _stripeCheckoutPaymentSettings;

		#endregion

		#region Ctor

		public StripeCheckoutPaymentProcessor(CurrencySettings currencySettings,
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
			StripeCheckoutHttpClient stripeCheckoutHttpClient,
			StripeCheckoutPaymentSettings stripeCheckoutPaymentSettings)
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
			_stripeCheckoutHttpClient = stripeCheckoutHttpClient;
			_stripeCheckoutPaymentSettings = stripeCheckoutPaymentSettings;
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

			try
			{
				var currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
				var currencyCode = currency?.CurrencyCode?.ToLowerInvariant() ?? "usd";
				var result = await _stripeCheckoutHttpClient.CreateSession(
					postProcessPaymentRequest.Order.OrderGuid, currencyCode, amount, reason);
				var sessionId = result.Item1;
				var url = result.Item2;

				await _genericAttributeService.SaveAttributeAsync<string>(postProcessPaymentRequest.Order, StripeCheckoutHelper.OrderAttributeStripeCheckoutSessionId, sessionId);

				_httpContextAccessor.HttpContext.Response.Redirect(url);
			}
			catch (Exception exception)
			{
				var errorString = string.Format(
					"Faild to create StripeCheckout session for order {0}. {1}",
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
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentStripeCheckout/Configure";
        }
        
        /// <summary>
        /// Gets a type of a view component for displaying plugin in public store
        /// </summary>
        /// <returns>View component type</returns>
        public Type GetPublicViewComponent()
        {
            return typeof(PaymentStripeCheckoutViewComponent);
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentStripeCheckout";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new StripeCheckoutPaymentSettings());

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.StripeCheckout.Fields.RedirectionTip"] = "You will be redirected to Stripe.com site to complete the order.",
                ["Plugins.Payments.StripeCheckout.Fields.ApiSecretKey"] = "API Secret Key",
                ["Plugins.Payments.StripeCheckout.Fields.ApiSecretKey.Hint"] = "Specify the secret key for the API",
                ["Plugins.Payments.StripeCheckout.Instructions"] = @"Configure the StripeCheckout API.",
                ["Plugins.Payments.StripeCheckout.PaymentMethodDescription"] = "You will be redirected to Stripe.com site to complete the payment"
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
            await _settingService.DeleteSettingAsync<StripeCheckoutPaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.StripeCheckout");

            await base.UninstallAsync();
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.StripeCheckout.PaymentMethodDescription");
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
