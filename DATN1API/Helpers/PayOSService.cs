using DATN1API.Helpers;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class PayOSService
{
    private readonly PayOSOptions _options;
    private readonly HttpClient _httpClient;

    public string LastError { get; private set; }

    public PayOSService(IOptions<PayOSOptions> options)
    {
        _options = options.Value;
        _httpClient = new HttpClient();
    }

    private static string GenerateSignature(string body, string key)
    {
        var encoding = Encoding.UTF8;
        var keyBytes = encoding.GetBytes(key);
        var bodyBytes = encoding.GetBytes(body);
        using (var hmac = new HMACSHA256(keyBytes))
        {
            var hash = hmac.ComputeHash(bodyBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    public async Task<string> CreatePaymentRequestAsync(decimal amount, long orderCode, string description, string returnUrl, string cancelUrl, List<PayOSItem> items)
    {
        int intAmount = Convert.ToInt32(Math.Round(amount));

        var payload = new PaymentRequest
        {
            orderCode = orderCode,
            amount = intAmount,
            description = description,
            returnUrl = returnUrl,
            cancelUrl = cancelUrl,
            items = items
        };

        // Serialize payload and generate signature
        var settings = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
        };

        string body = JsonConvert.SerializeObject(payload, settings);
        string signature = GenerateSignature(body, _options.ChecksumKey);

        var content = new StringContent(body, new UTF8Encoding(false), "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-client-id", _options.ClientId);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("x-signature", signature);

        var response = await _httpClient.PostAsync("https://api-merchant.payos.vn/v2/payment-requests", content);
        var result = await response.Content.ReadAsStringAsync();

        Console.WriteLine("📦 Raw PayOS Response: " + result);
        Console.WriteLine("Payload gửi đi: " + body);

        if (!response.IsSuccessStatusCode)
        {
            LastError = result;
            return null;
        }

        try
        {
            var json = JObject.Parse(result);
            return json["data"]?["qrCode"]?.ToString();
        }
        catch (Exception ex)
        {
            LastError = $"Lỗi JSON: {ex.Message}";
            return null;
        }
    }
    public async Task<JObject?> KiemTraTrangThaiThanhToan(long orderCode)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-client-id", _options.ClientId);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);

        var url = $"https://api-merchant.payos.vn/v2/payment-requests/{orderCode}";
        var response = await _httpClient.GetAsync(url);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            LastError = responseString;
            return null;
        }

        try
        {
            var result = JObject.Parse(responseString);
            return result;
        }
        catch (Exception ex)
        {
            LastError = $"Lỗi parse JSON: {ex.Message}";
            return null;
        }
    }

}
