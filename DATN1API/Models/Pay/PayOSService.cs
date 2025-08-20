using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using DATN1API.Models.Pay;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

public class PayOSService
{
    private readonly HttpClient _http;
    private readonly PayOSOptions _opt;
    public string? LastError { get; private set; }

    public PayOSService(HttpClient http, IOptions<PayOSOptions> opt)
    {
        _http = http;
        _opt = opt.Value;

        _http.BaseAddress = new Uri(_opt.BaseUrl.TrimEnd('/') + "/");
        // Header auth theo spec PayOS (thay đổi nếu docs khác)
        _http.DefaultRequestHeaders.Remove("X-Client-Id");
        _http.DefaultRequestHeaders.Remove("X-Api-Key");
        _http.DefaultRequestHeaders.Add("X-Client-Id", _opt.ClientId);
        _http.DefaultRequestHeaders.Add("X-Api-Key", _opt.ApiKey);
    }

    private string Sign(Dictionary<string, string> fields)
    {
        // sort by key asc, join "key=value&..." → HMACSHA256(ChecksumKey) → hex lower
        var raw = string.Join("&", fields.OrderBy(k => k.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.ChecksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    public async Task<string?> CreatePaymentRequestAsync(
        decimal total, long orderCode, string description, string returnUrl, string cancelUrl, List<PayOSItem> items)
    {
        try
        {
            var payload = new PayOSCreatePaymentRequest
            {
                orderCode = orderCode,
                amount = (long)total,
                description = description,
                returnUrl = returnUrl,
                cancelUrl = cancelUrl,
                items = items
            };

            var fields = new Dictionary<string, string>
            {
                ["orderCode"] = orderCode.ToString(),
                ["amount"] = ((long)total).ToString(),
                ["description"] = description,
                ["returnUrl"] = returnUrl,
                ["cancelUrl"] = cancelUrl
            };
            payload.signature = Sign(fields);

            // Endpoint thực tế theo PayOS
            var res = await _http.PostAsJsonAsync("v2/payment-requests", payload);
            var raw = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                LastError = raw;
                return null;
            }

            var data = await res.Content.ReadFromJsonAsync<PayOSCreatePaymentResponse>();
            return data?.qrCodeUrl ?? data?.checkoutUrl; // ưu tiên QR nếu có
        }
        catch (Exception ex)
        {
            LastError = ex.ToString();
            return null;
        }
    }

    public async Task<JObject?> GetPaymentStatusAsync(long orderCode)
    {
        try
        {
            var res = await _http.GetAsync($"v2/payment-requests/{orderCode}");
            var raw = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                LastError = raw;
                return null;
            }
            return JObject.Parse(raw);
        }
        catch (Exception ex)
        {
            LastError = ex.ToString();
            return null;
        }
    }

    public bool VerifyWebhook(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader)) return false;
        // Nhiều cổng dùng secret webhook riêng. Nếu PayOS dùng chung ChecksumKey, thay vào đây.
        var secret = string.IsNullOrEmpty(_opt.WebhookSecret) ? _opt.ChecksumKey : _opt.WebhookSecret;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var calc = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return string.Equals(calc, signatureHeader, StringComparison.OrdinalIgnoreCase);
    }
}
