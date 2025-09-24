using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class ShippingController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly string ghnApiKey = "66cea704-96c6-11f0-8873-9a3d2fab8c09"; // test token GHN

    public ShippingController(HttpClient http)
    {
        _http = http;
    }

    // ================= GHN Master Data =================
    [HttpGet("provinces")]
    public async Task<IActionResult> GetProvinces()
    {
        var req = new HttpRequestMessage(HttpMethod.Get,
            "https://online-gateway.ghn.vn/shiip/public-api/master-data/province");
        req.Headers.Add("Token", ghnApiKey);

        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    [HttpGet("districts/{provinceId}")]
    public async Task<IActionResult> GetDistricts(int provinceId)
    {
        var body = new { province_id = provinceId };
        var req = new HttpRequestMessage(HttpMethod.Post,
            "https://online-gateway.ghn.vn/shiip/public-api/master-data/district");
        req.Headers.Add("Token", ghnApiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    [HttpGet("wards/{districtId}")]
    public async Task<IActionResult> GetWards(int districtId)
    {
        var body = new { district_id = districtId };
        var req = new HttpRequestMessage(HttpMethod.Post,
            "https://online-gateway.ghn.vn/shiip/public-api/master-data/ward");
        req.Headers.Add("Token", ghnApiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    // ================= GHN Fee =================
    [HttpPost("calc")]
    public async Task<IActionResult> CalcFee([FromBody] ShippingFeeRequest req)
    {
        var body = new
        {
            service_type_id = 2, // dịch vụ tiêu chuẩn
            to_district_id = req.ToDistrictId,
            to_ward_code = req.ToWardCode,
            weight = req.Weight <= 0 ? 500 : req.Weight,
            length = req.Length <= 0 ? 20 : req.Length,
            width = req.Width <= 0 ? 15 : req.Width,
            height = req.Height <= 0 ? 5 : req.Height,
            insurance_value = req.InsuranceValue
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/fee");
        request.Headers.Add("Token", ghnApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(request);
        var json = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return BadRequest(new { success = false, message = json });

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var data) &&
            data.TryGetProperty("total", out var total))
        {
            return Ok(new { success = true, fee = total.GetInt32() });
        }

        return BadRequest(new { success = false, message = "Không lấy được phí từ GHN", raw = json });
    }
}

public class ShippingFeeRequest
{
    public int ToDistrictId { get; set; }
    public string ToWardCode { get; set; }
    public int Weight { get; set; }
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int InsuranceValue { get; set; }
}
