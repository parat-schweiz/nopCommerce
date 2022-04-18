using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Quaestur.Models;
using Nop.Plugin.Payments.Quaestur.Services;
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

namespace Nop.Plugin.Payments.Quaestur.Controllers
{
	[AutoValidateAntiforgeryToken]
	public class PaymentQuaesturController : BasePaymentController
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
		private readonly QuaesturHttpClient _quaesturHttpClient;

		#endregion

		#region Ctor

		public PaymentQuaesturController(IGenericAttributeService genericAttributeService,
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
			QuaesturHttpClient quaesturHttpClient)
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
			_quaesturHttpClient = quaesturHttpClient;
		}

		#endregion

		#region Methods

		[AuthorizeAdmin]
		[Area(AreaNames.ADMIN)]
		public async Task<IActionResult> Configure()
		{
			if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
				return AccessDeniedView();

			//load settings for a chosen store scope
			var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
			var quaesturPaymentSettings = await _settingService.LoadSettingAsync<QuaesturPaymentSettings>(storeScope);

			var model = new ConfigurationModel
			{
				ApiClientId = quaesturPaymentSettings.ApiClientId,
				ApiClientSecret = quaesturPaymentSettings.ApiClientSecret,
				ApiUrl = quaesturPaymentSettings.ApiUrl,

				ActiveStoreScopeConfiguration = storeScope
			};

			if (storeScope <= 0)
				return View("~/Plugins/Payments.Quaestur/Views/Configure.cshtml", model);

			return View("~/Plugins/Payments.Quaestur/Views/Configure.cshtml", model);
		}

		[HttpPost]
		[AuthorizeAdmin]
		[Area(AreaNames.ADMIN)]        
		public async Task<IActionResult> Configure(ConfigurationModel model)
		{
			if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
				return AccessDeniedView();

			if (!ModelState.IsValid)
				return await Configure();

			//load settings for a chosen store scope
			var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
			var quaesturPaymentSettings = await _settingService.LoadSettingAsync<QuaesturPaymentSettings>(storeScope);

			//save settings
			quaesturPaymentSettings.ApiClientId = model.ApiClientId;
			quaesturPaymentSettings.ApiClientSecret = model.ApiClientSecret;
			quaesturPaymentSettings.ApiUrl = model.ApiUrl;
			await _settingService.SaveSettingAsync(quaesturPaymentSettings);

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

			if (await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.Quaestur") is not QuaesturPaymentProcessor processor || !_paymentPluginManager.IsPluginActive(processor))
				throw new NopException("Quaestur Standard module cannot be loaded");

			var order = await GetOrder(orderIdString);
			if (order == null)
				return RedirectToAction("Index", "Home", new { area = string.Empty });

			var transactionId = await _genericAttributeService.GetAttributeAsync<string>(order, QuaesturHelper.OrderAttributeQuaesturTransactionId);

			try
			{
				await _quaesturHttpClient.CommitTransaction(transactionId);

				var infoString = string.Format(
					"Quaestur transaction {0} for order {1} commited.",
					transactionId,
					order.Id);
				await _logger.InformationAsync(infoString);
				await _orderService.InsertOrderNoteAsync(new OrderNote
				{
					OrderId = order.Id,
					Note = infoString,
					DisplayToCustomer = false,
					CreatedOnUtc = DateTime.UtcNow
				});

				order.AuthorizationTransactionId = transactionId;
				await _orderService.UpdateOrderAsync(order);
				await _orderProcessingService.MarkOrderAsPaidAsync(order);
				await _genericAttributeService.SaveAttributeAsync<string>(order, QuaesturHelper.OrderAttributeQuaesturTransactionId, null);

				return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
			}
			catch (ApiException exception)
			{
				var errorString = string.Format(
					"Faild to commit Quaestur transaction {0} for order {1}. {2}",
					transactionId,
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
				await _genericAttributeService.SaveAttributeAsync<string>(order, QuaesturHelper.OrderAttributeQuaesturTransactionId, null);

				return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
			}
		}

		public async Task<IActionResult> CanceledHandler()
		{
			var orderIdString = _webHelper.QueryString<string>("orderid");

			if (await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.Quaestur") is not QuaesturPaymentProcessor processor || !_paymentPluginManager.IsPluginActive(processor))
				throw new NopException("Quaestur Standard module cannot be loaded");

			var order = await GetOrder(orderIdString);
			if (order == null)
				return RedirectToAction("Index", "Home", new { area = string.Empty });

			var transactionId = await _genericAttributeService.GetAttributeAsync<string>(order, QuaesturHelper.OrderAttributeQuaesturTransactionId);

			var infoString = string.Format(
				"Customer canceled Quaestur transaction {0} for order {1}",
				transactionId,
				order.Id);

			await _logger.InformationAsync(infoString);
			await _orderService.InsertOrderNoteAsync(new OrderNote
			{
				OrderId = order.Id,
				Note = infoString,
				DisplayToCustomer = false,
				CreatedOnUtc = DateTime.UtcNow
			});
			await _genericAttributeService.SaveAttributeAsync<string>(order, QuaesturHelper.OrderAttributeQuaesturTransactionId, null);

			return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
		}

		#endregion
	}
}
