using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.StripeCheckout.Models;
using Nop.Plugin.Payments.StripeCheckout.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.StripeCheckout.Controllers
{
	[AutoValidateAntiforgeryToken]
	public class PaymentStripeCheckoutController : BasePaymentController
	{
		#region Fields

		private readonly IGenericAttributeService _genericAttributeService;
		private readonly IOrderProcessingService _orderProcessingService;
		private readonly IOrderService _orderService;
		private readonly IPaymentPluginManager _paymentPluginManager;
		private readonly IPermissionService _permissionService;
		private readonly ILocalizationService _localizationService;
		private readonly ILogger _logger;
		private readonly INotificationService _notificationService;
		private readonly ISettingService _settingService;
		private readonly IStoreContext _storeContext;
		private readonly IWebHelper _webHelper;
		private readonly IWorkContext _workContext;
		private readonly ShoppingCartSettings _shoppingCartSettings;
		private readonly StripeCheckoutHttpClient _stripeCheckoutHttpClient;

		#endregion

		#region Ctor

		public PaymentStripeCheckoutController(IGenericAttributeService genericAttributeService,
			IOrderProcessingService orderProcessingService,
			IOrderService orderService,
			IPaymentPluginManager paymentPluginManager,
			IPermissionService permissionService,
			ILocalizationService localizationService,
			ILogger logger,
			INotificationService notificationService,
			ISettingService settingService,
			IStoreContext storeContext,
			IWebHelper webHelper,
			IWorkContext workContext,
			ShoppingCartSettings shoppingCartSettings,
			StripeCheckoutHttpClient stripeCheckoutHttpClient)
		{
			_genericAttributeService = genericAttributeService;
			_orderProcessingService = orderProcessingService;
			_orderService = orderService;
			_paymentPluginManager = paymentPluginManager;
			_permissionService = permissionService;
			_localizationService = localizationService;
			_logger = logger;
			_notificationService = notificationService;
			_settingService = settingService;
			_storeContext = storeContext;
			_webHelper = webHelper;
			_workContext = workContext;
			_shoppingCartSettings = shoppingCartSettings;
			_stripeCheckoutHttpClient = stripeCheckoutHttpClient;
		}

		#endregion

		#region Methods

		[AuthorizeAdmin]
		[Area(AreaNames.Admin)]
		public async Task<IActionResult> Configure()
		{
			if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
				return AccessDeniedView();

			//load settings for a chosen store scope
			var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
			var stripeCheckoutPaymentSettings = await _settingService.LoadSettingAsync<StripeCheckoutPaymentSettings>(storeScope);

			var model = new ConfigurationModel
			{
				ApiSecretKey = stripeCheckoutPaymentSettings.ApiSecretKey,

				ActiveStoreScopeConfiguration = storeScope
			};

			if (storeScope <= 0)
				return View("~/Plugins/Payments.StripeCheckout/Views/Configure.cshtml", model);

			return View("~/Plugins/Payments.StripeCheckout/Views/Configure.cshtml", model);
		}

		[HttpPost]
		[AuthorizeAdmin]
		[Area(AreaNames.Admin)]        
		public async Task<IActionResult> Configure(ConfigurationModel model)
		{
			if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
				return AccessDeniedView();

			if (!ModelState.IsValid)
				return await Configure();

			//load settings for a chosen store scope
			var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
			var stripeCheckoutPaymentSettings = await _settingService.LoadSettingAsync<StripeCheckoutPaymentSettings>(storeScope);

			//save settings
			stripeCheckoutPaymentSettings.ApiSecretKey = model.ApiSecretKey;
			await _settingService.SaveSettingAsync(stripeCheckoutPaymentSettings);

			//now clear settings cache
			await _settingService.ClearCacheAsync();

			_notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

			return await Configure();
		}

		private async Task<Order> GetOrder(string orderIdString)
		{
			if (Guid.TryParse(orderIdString, out Guid orderId))
			{
				return await _orderService.GetOrderByGuidAsync(orderId);
			}
			else
			{
				return null;
			}
		}

		public async Task<IActionResult> PayedHandler()
		{
			var orderIdString = _webHelper.QueryString<string>("orderid");

			if (await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.StripeCheckout") is not StripeCheckoutPaymentProcessor processor || !_paymentPluginManager.IsPluginActive(processor))
				throw new NopException("StripeCheckout module cannot be loaded");

			var order = await GetOrder(orderIdString);
			if (order == null)
				return RedirectToAction("Index", "Home", new { area = string.Empty });

			var sessionId = await _genericAttributeService.GetAttributeAsync<string>(order, StripeCheckoutHelper.OrderAttributeStripeCheckoutSessionId);

			try
			{
				bool payed = await _stripeCheckoutHttpClient.GetSessionPayed(sessionId);
				if (!payed) throw new Exception("Customer did not pay.");

				var infoString = string.Format(
					"StripeCheckout session {0} for order {1} is payed.",
					sessionId,
					order.Id);
				await _logger.InformationAsync(infoString);
				await _orderService.InsertOrderNoteAsync(new OrderNote
				{
					OrderId = order.Id,
					Note = infoString,
					DisplayToCustomer = false,
					CreatedOnUtc = DateTime.UtcNow
				});

				order.AuthorizationTransactionId = sessionId;
				await _orderService.UpdateOrderAsync(order);
				await _orderProcessingService.MarkOrderAsPaidAsync(order);
				await _genericAttributeService.SaveAttributeAsync<string>(order, StripeCheckoutHelper.OrderAttributeStripeCheckoutSessionId, null);

				return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
			}
			catch (Exception exception)
			{
				var errorString = string.Format(
					"StripeCheckout session {0} for order {1} failed. {2}",
					sessionId,
					order.Id,
					exception.Message);

				await _logger.ErrorAsync(errorString);
				await _orderService.InsertOrderNoteAsync(new OrderNote
				{
					OrderId = order.Id,
					Note = errorString,
					DisplayToCustomer = false,
					CreatedOnUtc = DateTime.UtcNow
				});
				await _genericAttributeService.SaveAttributeAsync<string>(order, StripeCheckoutHelper.OrderAttributeStripeCheckoutSessionId, null);

				return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
			}
		}

		public async Task<IActionResult> CanceledHandler()
		{
			var orderIdString = _webHelper.QueryString<string>("orderid");

			if (await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.StripeCheckout") is not StripeCheckoutPaymentProcessor processor || !_paymentPluginManager.IsPluginActive(processor))
				throw new NopException("StripeCheckout module cannot be loaded");

			var order = await GetOrder(orderIdString);
			if (order == null)
				return RedirectToAction("Index", "Home", new { area = string.Empty });

			var sessionId = await _genericAttributeService.GetAttributeAsync<string>(order, StripeCheckoutHelper.OrderAttributeStripeCheckoutSessionId);

			var infoString = string.Format(
				"Customer canceled StripeCheckout session {0} for order {1}",
				sessionId,
				order.Id);

			await _logger.InformationAsync(infoString);
			await _orderService.InsertOrderNoteAsync(new OrderNote
			{
				OrderId = order.Id,
				Note = infoString,
				DisplayToCustomer = false,
				CreatedOnUtc = DateTime.UtcNow
			});
			await _genericAttributeService.SaveAttributeAsync<string>(order, StripeCheckoutHelper.OrderAttributeStripeCheckoutSessionId, null);

			return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
		}

		#endregion
	}
}
