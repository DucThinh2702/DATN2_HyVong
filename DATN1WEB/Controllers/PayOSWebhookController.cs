using DATN1API.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

[ApiController]
[Route("payos/webhook")]
public class PayOSWebhookController : ControllerBase
{
    private readonly PayOSService _payOS;
    private readonly DatnContext _context;

    public PayOSWebhookController(PayOSService payOS, DatnContext context)
    {
        _payOS = payOS;
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Payos-Signature"].FirstOrDefault(); // header tên gì tùy docs PayOS

        if (!_payOS.VerifyWebhook(raw, signature))
            return Unauthorized();

        var json = JObject.Parse(raw);
        var orderCode = json["data"]?["orderCode"]?.Value<long>() ?? 0;
        var status = json["data"]?["status"]?.ToString();

        // TODO:
        // - Tìm Order theo orderCode
        // - Nếu status == "PAID" và Order chưa PAID → set Paid, giảm tồn kho, gửi mail…
        // - Nếu status == "CANCELLED"/"FAILED" → update tương ứng
        // - Lưu log raw để truy vết

        return Ok(new { received = true });
    }
}
