using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;

namespace Nop.Plugin.Payments.Quaestur.Services
{
	public class ApiException : Exception
	{
		public ApiException(string message)
		: base(message)
		{ }
	}

	/// <summary>
	/// Represents the HTTP client to request Quaestur services
	/// </summary>
	public partial class QuaesturHttpClient
	{
		#region Fields

		private readonly HttpClient _httpClient;
		private readonly IWebHelper _webHelper;
		private readonly QuaesturPaymentSettings _quaesturPaymentSettings;

		#endregion

		#region Ctor

		public QuaesturHttpClient(HttpClient client,
			IWebHelper webHelper,
			QuaesturPaymentSettings quaesturPaymentSettings)
		{
			//configure client
			client.Timeout = TimeSpan.FromSeconds(20);
			client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, $"nopCommerce-{NopVersion.CURRENT_VERSION}");

			var authValue = string.Format(
				"QAPI2 {0} {1}",
				quaesturPaymentSettings.ApiClientId, 
				quaesturPaymentSettings.ApiClientSecret); 
			client.DefaultRequestHeaders.Add("Authorization", authValue);

			_httpClient = client;
			_webHelper = webHelper;
			_quaesturPaymentSettings = quaesturPaymentSettings;
		}

		#endregion

		#region Methods

		private string QuaesturApiUrl(string pathFormat, params string[] parameters)
		{
			var path = string.Format(pathFormat, parameters);
		return _quaesturPaymentSettings.ApiUrl + path;
		}

		private async Task<JObject> DoRequest(string url, JObject requestObject)
		{
			var requestContent = new StringContent(requestObject.ToString(),
				Encoding.UTF8, MimeTypes.ApplicationXWwwFormUrlencoded);
			var response = await _httpClient.PostAsync(url, requestContent);
			response.EnsureSuccessStatusCode();
			var responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());

			if (responseObject.Value<string>("status") != "success")
			{
				var error = responseObject.Value<string>("error");
				throw new ApiException("Quaestur API error: " + error); 
			}

			return responseObject;
		}

		public async Task<string> PrepareTransaction(Guid orderId, decimal amount, string reason, string url)
		{
			var payReturnUrl = string.Format(
				"{0}Plugins/PaymentQuaestur/Payed?orderid={1}",
				_webHelper.GetStoreLocation(),
				orderId);
			var cancelReturnUrl = string.Format(
				"{0}Plugins/PaymentQuaestur/Canceled?orderid={1}",
				_webHelper.GetStoreLocation(),
				orderId);
			var requestObject = new JObject(
				new JProperty("amount", amount),
				new JProperty("reason", reason),
				new JProperty("url", url),
				new JProperty("payreturnurl", payReturnUrl),
				new JProperty("cancelreturnurl", cancelReturnUrl));
			var apiUrl = QuaesturApiUrl("/api/v2/payment/prepare");
			var responseObject = await DoRequest(apiUrl, requestObject);
			return responseObject.Value<string>("id");
		}

		public async Task CommitTransaction(string id)
		{
			var requestObject = new JObject(
				new JProperty("id", id));
			var apiUrl = QuaesturApiUrl("/api/v2/payment/commit");
			await DoRequest(apiUrl, requestObject);
		}

		#endregion
	}
}
