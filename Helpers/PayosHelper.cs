using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ApiKey.Helpers
{
    public static class PayosHelper
    {
        private static readonly string ClientId = ConfigurationManager.AppSettings["PayosClientId"];
        private static readonly string ApiKeyString = ConfigurationManager.AppSettings["PayosApiKey"];
        private static readonly string ChecksumKey = ConfigurationManager.AppSettings["PayosChecksumKey"];
        private static readonly string BaseUrl = ConfigurationManager.AppSettings["PayosBaseUrl"]?.TrimEnd('/');

        private static readonly HttpClient HttpClientInstance = new HttpClient();

        public static bool IsConfigured()
        {
            return !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ApiKeyString) && !string.IsNullOrEmpty(ChecksumKey);
        }

        private static string ComputeHmacSha256(string rawData, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] dataBytes = Encoding.UTF8.GetBytes(rawData);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(dataBytes);
                StringBuilder builder = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string GetPaymentRequestSignature(long orderCode, int amount, string description, string cancelUrl, string returnUrl)
        {
            string raw = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
            return ComputeHmacSha256(raw, ChecksumKey);
        }

        public class PaymentRequestItem
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("quantity")]
            public int Quantity { get; set; }
            [JsonProperty("price")]
            public int Price { get; set; }
        }

        public class PaymentRequestData
        {
            [JsonProperty("orderCode")]
            public long OrderCode { get; set; }
            [JsonProperty("amount")]
            public int Amount { get; set; }
            [JsonProperty("description")]
            public string Description { get; set; }
            [JsonProperty("cancelUrl")]
            public string CancelUrl { get; set; }
            [JsonProperty("returnUrl")]
            public string ReturnUrl { get; set; }
            [JsonProperty("signature")]
            public string Signature { get; set; }
            [JsonProperty("items")]
            public List<PaymentRequestItem> Items { get; set; }
        }

        public class PayosResponse<T>
        {
            [JsonProperty("code")]
            public string Code { get; set; }
            [JsonProperty("desc")]
            public string Desc { get; set; }
            [JsonProperty("data")]
            public T Data { get; set; }
        }

        public class CreatePaymentResult
        {
            [JsonProperty("bin")]
            public string Bin { get; set; }
            [JsonProperty("accountNumber")]
            public string AccountNumber { get; set; }
            [JsonProperty("accountName")]
            public string AccountName { get; set; }
            [JsonProperty("amount")]
            public int Amount { get; set; }
            [JsonProperty("description")]
            public string Description { get; set; }
            [JsonProperty("orderCode")]
            public long OrderCode { get; set; }
            [JsonProperty("paymentLinkId")]
            public string PaymentLinkId { get; set; }
            [JsonProperty("status")]
            public string Status { get; set; }
            [JsonProperty("checkoutUrl")]
            public string CheckoutUrl { get; set; }
            [JsonProperty("qrCode")]
            public string QrCode { get; set; }
        }

        public class PaymentDetails
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            [JsonProperty("orderCode")]
            public long OrderCode { get; set; }
            [JsonProperty("amount")]
            public int Amount { get; set; }
            [JsonProperty("amountPaid")]
            public int AmountPaid { get; set; }
            [JsonProperty("amountRemaining")]
            public int AmountRemaining { get; set; }
            [JsonProperty("status")]
            public string Status { get; set; }
            [JsonProperty("createdAt")]
            public string CreatedAt { get; set; }
        }

        public static async Task<CreatePaymentResult> CreatePaymentLink(long orderCode, int amount, string description, string cancelUrl, string returnUrl, string productName)
        {
            if (!IsConfigured())
            {
                throw new Exception("PayOS has not been properly configured in Web.config.");
            }

            string signature = GetPaymentRequestSignature(orderCode, amount, description, cancelUrl, returnUrl);

            var requestData = new PaymentRequestData
            {
                OrderCode = orderCode,
                Amount = amount,
                Description = description,
                CancelUrl = cancelUrl,
                ReturnUrl = returnUrl,
                Signature = signature,
                Items = new List<PaymentRequestItem>
                {
                    new PaymentRequestItem
                    {
                        Name = productName,
                        Quantity = 1,
                        Price = amount
                    }
                }
            };

            string jsonBody = JsonConvert.SerializeObject(requestData);

            using (var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/payment-requests"))
            {
                request.Headers.Add("x-client-id", ClientId);
                request.Headers.Add("x-api-key", ApiKeyString);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using (var response = await HttpClientInstance.SendAsync(request))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"PayOS payment request failed with status: {response.StatusCode}. Output: {responseBody}");
                    }

                    var payosResponse = JsonConvert.DeserializeObject<PayosResponse<CreatePaymentResult>>(responseBody);
                    if (payosResponse == null || payosResponse.Code != "00")
                    {
                        throw new Exception($"PayOS API returned code {payosResponse?.Code}: {payosResponse?.Desc}");
                    }

                    return payosResponse.Data;
                }
            }
        }

        public static async Task<PaymentDetails> GetPaymentLinkInformation(long orderCode)
        {
            if (!IsConfigured())
            {
                throw new Exception("PayOS has not been properly configured in Web.config.");
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/v2/payment-requests/{orderCode}"))
            {
                request.Headers.Add("x-client-id", ClientId);
                request.Headers.Add("x-api-key", ApiKeyString);

                using (var response = await HttpClientInstance.SendAsync(request))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"PayOS detail request failed with status: {response.StatusCode}. Output: {responseBody}");
                    }

                    var payosResponse = JsonConvert.DeserializeObject<PayosResponse<PaymentDetails>>(responseBody);
                    if (payosResponse == null || payosResponse.Code != "00")
                    {
                        throw new Exception($"PayOS API returned code {payosResponse?.Code}: {payosResponse?.Desc}");
                    }

                    return payosResponse.Data;
                }
            }
        }
    }
}
