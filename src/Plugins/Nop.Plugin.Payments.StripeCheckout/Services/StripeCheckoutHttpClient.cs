using System;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;

namespace Nop.Plugin.Payments.StripeCheckout.Services
{
	public class ApiException : Exception
	{
		public ApiException(string message)
		: base(message)
		{ }
	}

	/// <summary>
	/// Represents the HTTP client to request StripeCheckout services
	/// </summary>
	public partial class StripeCheckoutHttpClient
	{
		#region Fields

		private readonly HttpClient _httpClient;
		private readonly IWebHelper _webHelper;
		private readonly StripeCheckoutPaymentSettings _stripeCheckoutPaymentSettings;

		#endregion

		#region Ctor

		public StripeCheckoutHttpClient(HttpClient client,
			IWebHelper webHelper,
			StripeCheckoutPaymentSettings stripeCheckoutPaymentSettings)
		{
			//configure client
			client.Timeout = TimeSpan.FromSeconds(20);
			client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, $"nopCommerce-{NopVersion.CURRENT_VERSION}");

			if (!string.IsNullOrEmpty(stripeCheckoutPaymentSettings.ApiSecretKey))
			{
				var authValue = string.Format(
					"Basic {0}",
					Convert.ToBase64String(Encoding.UTF8.GetBytes(stripeCheckoutPaymentSettings.ApiSecretKey))); 
				client.DefaultRequestHeaders.Add("Authorization", authValue);
			}

			_httpClient = client;
			_webHelper = webHelper;
			_stripeCheckoutPaymentSettings = stripeCheckoutPaymentSettings;
		}

		#endregion

		#region Methods

		private async Task<JObject> PostRequest(string url, IEnumerable<KeyValuePair<string, string>> postData)
		{
			var requestContent = new FormUrlEncodedContent(postData);
			var response = await _httpClient.PostAsync(url, requestContent);
			response.EnsureSuccessStatusCode();
			return JObject.Parse(await response.Content.ReadAsStringAsync());
		}

		private async Task<JObject> GetRequest(string url)
		{
			var response = await _httpClient.GetAsync(url);
			response.EnsureSuccessStatusCode();
			return JObject.Parse(await response.Content.ReadAsStringAsync());
		}

		public async Task<Tuple<string, string>> CreateSession(Guid orderId, string currency, decimal amount, string reason)
		{
			var payReturnUrl = string.Format(
				"{0}Plugins/PaymentStripeCheckout/Payed?orderid={1}",
				_webHelper.GetStoreLocation(),
				orderId);
			var cancelReturnUrl = string.Format(
				"{0}Plugins/PaymentStripeCheckout/Canceled?orderid={1}",
				_webHelper.GetStoreLocation(),
				orderId);
			var postData = new Dictionary<string, string>();
			postData.Add("success_url", payReturnUrl);
			postData.Add("cancel_url", cancelReturnUrl);
			postData.Add("mode", "payment");
			postData.Add("line_items[0][price_data][currency]", currency);
			postData.Add("line_items[0][price_data][product_data][name]", reason);
			postData.Add("line_items[0][price_data][unit_amount_decimal]", (amount * 100M).ToString());
			postData.Add("line_items[0][quantity]", "1");
			var apiUrl = "https://api.stripe.com/v1/checkout/sessions";
			var responseObject = await PostRequest(apiUrl, postData);
			var id = responseObject.Value<string>("id");
			var url = responseObject.Value<string>("url");
			return new Tuple<string, string>(id, url);
		}

		public async Task<bool> GetSessionPayed(string id)
		{
			var apiUrl = "https://api.stripe.com/v1/checkout/sessions/" + id;
			var responseObject = await GetRequest(apiUrl);
			return responseObject.Value<string>("payment_status") == "paid";
		}

		#endregion
	}
}
