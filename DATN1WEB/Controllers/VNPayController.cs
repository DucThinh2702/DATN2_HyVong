using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using DATN1API.Models;
using DATN1API.Data; // DbContext của bạn
using Microsoft.AspNetCore.Http;

namespace DATNAPI1.Controllers
{
    public class VNPayController : Controller
    {
        private readonly ILogger<VNPayController> _logger;
        private readonly IConfiguration _config;
        private readonly DatnContext _db;

        private readonly string _vnp_TmnCode;
        private readonly string _vnp_HashSecret;
        private readonly string _vnp_Url;
        private readonly string _vnp_ReturnUrl;

        public VNPayController(
            IConfiguration configuration,
            ILogger<VNPayController> logger,
            DatnContext db
        )
        {
            _config = configuration;
            _logger = logger;
            _db = db;

            _vnp_TmnCode = (_config["VNPay:TmnCode"] ?? throw new InvalidOperationException("Missing VNPay:TmnCode")).Trim();
            _vnp_HashSecret = (_config["VNPay:HashSecret"] ?? throw new InvalidOperationException("Missing VNPay:HashSecret")).Trim();
            _vnp_Url = (_config["VNPay:Url"] ?? throw new InvalidOperationException("Missing VNPay:Url")).Trim().TrimEnd('?');
            _vnp_ReturnUrl = (_config["VNPay:ReturnUrl"] ?? throw new InvalidOperationException("Missing VNPay:ReturnUrl")).Trim();

            _logger.LogInformation("VNPay config loaded. TmnCode={tmn}, Url={url}, ReturnUrl={ret}",
                _vnp_TmnCode, _vnp_Url, _vnp_ReturnUrl);
        }

        public IActionResult Payment() => View();

        // 👉 Tính tổng server-side + redirect VNPAY
        [HttpPost, Authorize]
        public async Task<IActionResult> CreatePaymentFromCart(string? orderNote = null, string? orderType = "other")
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "(unknown)";
                var cart = await _db.Carts
                    .Include(c => c.CartDetails)
                        .ThenInclude(cd => cd.ProductVariant)
                            .ThenInclude(pv => pv.Product)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null || !cart.CartDetails.Any())
                {
                    TempData["Message"] = "Giỏ hàng rỗng.";
                    _logger.LogWarning("CreatePaymentFromCart: Empty cart. userId={userId}", userId);
                    return RedirectToAction("GioHang", "User");
                }

                var total = cart.CartDetails.Sum(cd => (cd.ProductVariant.SalePrice ?? 0m) * (cd.Quantity ?? 1));
                var amountVnd = (int)decimal.Round(total, 0, MidpointRounding.AwayFromZero);
                var orderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                _logger.LogInformation("CreatePaymentFromCart: userId={userId}, items={count}, total={totalVnd}, orderId={orderId}",
                    userId, cart.CartDetails.Count, amountVnd, orderId);

                var vnp = new VnPayLibrary();

                // Tham số bắt buộc
                vnp.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
                vnp.AddRequestData("vnp_Command", "pay");
                vnp.AddRequestData("vnp_TmnCode", _vnp_TmnCode);
                vnp.AddRequestData("vnp_Amount", (amountVnd * 100).ToString(CultureInfo.InvariantCulture)); // invariant
                vnp.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                vnp.AddRequestData("vnp_CurrCode", "VND");
                vnp.AddRequestData("vnp_IpAddr", GetClientIp(HttpContext));
                vnp.AddRequestData("vnp_Locale", "vn");
                vnp.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {orderId}");
                vnp.AddRequestData("vnp_OrderType", string.IsNullOrWhiteSpace(orderType) ? "other" : orderType);
                vnp.AddRequestData("vnp_ReturnUrl", _vnp_ReturnUrl);
                vnp.AddRequestData("vnp_TxnRef", orderId.ToString());
                vnp.AddRequestData("vnp_ExpireDate", DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"));

                // Optional: BankCode...
                // vnp.AddRequestData("vnp_BankCode", "VNBANK");

                // Tạo URL + lấy hashData/hmac để đối chiếu
                var paymentUrl = vnp.CreateRequestUrlAndGetDebug(_vnp_Url, _vnp_HashSecret, out var hashData, out var myHashUpper);

                _logger.LogInformation("VNPay hashData: {hashData}", hashData);
                _logger.LogInformation("VNPay myHMAC (UpperHex): {myHashUpper}", myHashUpper);
                _logger.LogInformation("VNPAY URL: {url}", paymentUrl);

                return Redirect(paymentUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreatePaymentFromCart failed.");
                TempData["Message"] = "Không thể tạo thanh toán. Vui lòng thử lại.";
                return RedirectToAction("GioHang", "User");
            }
        }

        // ========== THANH TOÁN VNPAY TỪ VIEW THANH TOÁN ==========
        [HttpPost, Authorize]
        public async Task<IActionResult> CreatePaymentFromCheckout([FromBody] CheckoutPaymentRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "(unknown)";

                var appUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (appUser == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
                }

                if (request.Items == null || !request.Items.Any())
                {
                    return Json(new { success = false, message = "Không có sản phẩm được chọn." });
                }

                if (string.IsNullOrWhiteSpace(request.CustomerName) ||
                    string.IsNullOrWhiteSpace(request.CustomerPhone) ||
                    string.IsNullOrWhiteSpace(request.FullAddress))
                {
                    return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin giao hàng." });
                }

                var total = request.Items.Sum(item => item.Price * item.Quantity);
                var amountVnd = (int)decimal.Round(total, 0, MidpointRounding.AwayFromZero);
                var orderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var orderData = new
                {
                    UserId = userId,
                    CustomerName = request.CustomerName,
                    CustomerPhone = request.CustomerPhone,
                    FullAddress = request.FullAddress,
                    Items = request.Items,
                    Total = total,
                    OrderNote = request.OrderNote,
                    OrderId = orderId
                };

                HttpContext.Session.SetString($"PendingOrder_{orderId}", JsonSerializer.Serialize(orderData));

                _logger.LogInformation("CreatePaymentFromCheckout: userId={userId}, items={count}, total={totalVnd}, orderId={orderId}",
                    userId, request.Items.Count, amountVnd, orderId);

                var vnp = new VnPayLibrary();

                // Tham số bắt buộc
                vnp.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
                vnp.AddRequestData("vnp_Command", "pay");
                vnp.AddRequestData("vnp_TmnCode", _vnp_TmnCode);
                vnp.AddRequestData("vnp_Amount", (amountVnd * 100).ToString(CultureInfo.InvariantCulture));
                vnp.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                vnp.AddRequestData("vnp_CurrCode", "VND");
                vnp.AddRequestData("vnp_IpAddr", GetClientIp(HttpContext));
                vnp.AddRequestData("vnp_Locale", "vn");
                vnp.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {orderId}");
                vnp.AddRequestData("vnp_OrderType", "other");
                vnp.AddRequestData("vnp_ReturnUrl", _vnp_ReturnUrl);
                vnp.AddRequestData("vnp_TxnRef", orderId.ToString());
                vnp.AddRequestData("vnp_ExpireDate", DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"));

                var paymentUrl = vnp.CreateRequestUrlAndGetDebug(_vnp_Url, _vnp_HashSecret, out var hashData, out var myHashUpper);

                _logger.LogInformation("VNPay hashData: {hashData}", hashData);
                _logger.LogInformation("VNPay myHMAC (UpperHex): {myHashUpper}", myHashUpper);
                _logger.LogInformation("VNPAY URL: {url}", paymentUrl);

                return Json(new { success = true, paymentUrl = paymentUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreatePaymentFromCheckout failed.");
                return Json(new { success = false, message = "Không thể tạo thanh toán. Vui lòng thử lại." });
            }
        }

        // VNPay callback
        [HttpGet]
        public IActionResult PaymentReturn()
        {
            try
            {
                var rawQuery = HttpContext.Request.QueryString.Value ?? string.Empty;
                var vnp_SecureHash = Request.Query["vnp_SecureHash"].ToString();

                if (string.IsNullOrEmpty(vnp_SecureHash))
                {
                    _logger.LogWarning("PaymentReturn: Missing vnp_SecureHash. rawQuery={raw}", rawQuery);
                    ViewBag.Message = "Thiếu chữ ký bảo mật";
                    return View();
                }

                var valid = VnPayLibrary.ValidateSignatureFromRaw(rawQuery, vnp_SecureHash, _vnp_HashSecret,
                                                                  out var rawForHash, out var myHashUpper);

                _logger.LogInformation("VNPay callback rawForHash: {rawForHash}", rawForHash);
                _logger.LogInformation("VNPay callback myHMAC(UpperHex): {myHashUpper}", myHashUpper);
                _logger.LogInformation("VNPay callback their vnp_SecureHash: {their}", vnp_SecureHash);

                if (!valid)
                {
                    _logger.LogWarning("VNPay signature invalid.");
                    ViewBag.Message = "⚠ Sai chữ ký bảo mật!";
                    return View();
                }

                // Đến đây: chữ ký OK
                var responseCode = Request.Query["vnp_ResponseCode"].ToString();
                var txnRef = Request.Query["vnp_TxnRef"].ToString();
                var amount = Request.Query["vnp_Amount"].ToString();
                var bankTranNo = Request.Query["vnp_BankTranNo"].ToString();
                var payDate = Request.Query["vnp_PayDate"].ToString();
                var transactionStatus = Request.Query["vnp_TransactionStatus"].ToString();

                _logger.LogInformation("VNPay callback params: resp={resp}, txnRef={ref}, amount={amt}, bankTranNo={bank}, payDate={pay}, txnStatus={st}",
                    responseCode, txnRef, amount, bankTranNo, payDate, transactionStatus);

                if (responseCode == "00")
                {
                    ViewBag.Message = "✅ Thanh toán thành công!";
                    // TODO: cập nhật Order theo txnRef (idempotent), clear cart...
                }
                else
                {
                    ViewBag.Message = $"❌ Thanh toán thất bại. Mã lỗi: {responseCode}";
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PaymentReturn failed.");
                ViewBag.Message = "Có lỗi khi xử lý kết quả thanh toán.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> VnPayReturn()
        {
            try
            {
                var vnpayData = HttpContext.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());

                var vnp = new VnPayLibrary();
                foreach (var (key, value) in vnpayData)
                {
                    if (!string.IsNullOrEmpty(value) && key.StartsWith("vnp_"))
                    {
                        vnp.AddResponseData(key, value);
                    }
                }

                var orderId = Convert.ToInt64(vnp.GetResponseData("vnp_TxnRef"));
                var vnpayTranId = Convert.ToInt64(vnp.GetResponseData("vnp_TransactionNo"));
                var vnp_ResponseCode = vnp.GetResponseData("vnp_ResponseCode");
                var vnp_TransactionStatus = vnp.GetResponseData("vnp_TransactionStatus");
                var vnp_SecureHash = HttpContext.Request.Query["vnp_SecureHash"];
                var terminalID = HttpContext.Request.Query["vnp_TmnCode"];
                var bankCode = HttpContext.Request.Query["vnp_BankCode"];
                var amount = Convert.ToInt64(vnp.GetResponseData("vnp_Amount")) / 100;

                bool checkSignature = vnp.ValidateSignature(vnp_SecureHash, _vnp_HashSecret);
                if (!checkSignature)
                {
                    ViewBag.Message = "Chữ ký không hợp lệ";
                    return View();
                }

                if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                {
                    var sessionKey = $"PendingOrder_{orderId}";
                    var orderDataJson = HttpContext.Session.GetString(sessionKey);

                    if (!string.IsNullOrEmpty(orderDataJson))
                    {
                        var orderData = JsonSerializer.Deserialize<JsonElement>(orderDataJson);
                        await SaveOrderAndPayment(orderData, vnpayTranId, amount);
                        HttpContext.Session.Remove(sessionKey); // Xóa session sau khi lưu thành công
                    }

                    ViewBag.Message = "Giao dịch được thực hiện thành công. Cảm ơn quý khách đã sử dụng dịch vụ";
                }
                else
                {
                    ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý. Mã lỗi: " + vnp_ResponseCode;
                }

                ViewBag.ThanhToanThanhCong = (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VnPayReturn failed.");
                ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý";
                return View();
            }
        }

        private async Task SaveOrderAndPayment(JsonElement orderData, long vnpayTranId, long amount)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var userId = orderData.GetProperty("UserId").GetString();

                // Tạo đơn hàng
                var order = new Order
                {
                    UserId = userId,
                    RecipientName = orderData.GetProperty("CustomerName").GetString(),
                    RecipientPhone = orderData.GetProperty("CustomerPhone").GetString(),
                    DeliveryAddress = orderData.GetProperty("FullAddress").GetString(),
                    OrderDate = DateTime.Now,
                    TotalAmount = orderData.GetProperty("Total").GetDecimal(),
                    OrderStatus = "Chờ xác nhận",
                    PaymentStatus = "Đã thanh toán",
                    Note = orderData.TryGetProperty("OrderNote", out var noteProperty) ? noteProperty.GetString() : null
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync(); // Lưu để có OrderId

                // Tạo chi tiết đơn hàng
                var items = orderData.GetProperty("Items").EnumerateArray();
                foreach (var item in items)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        ProductVariantId = item.GetProperty("VariantId").GetInt32(),
                        Quantity = item.GetProperty("Quantity").GetInt32(),
                        UnitPrice = item.GetProperty("Price").GetDecimal(),
                        TotalPrice = item.GetProperty("Price").GetDecimal() * item.GetProperty("Quantity").GetInt32()
                    };
                    _db.OrderDetails.Add(orderDetail);
                }

                // Tạo bản ghi thanh toán
                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    MethodName = "VNPay",
                    PaymentDate = DateTime.Now,
                    Amount = amount,
                    PaymentStatus = "Thành công",
                    BankTransactionCode = vnpayTranId.ToString(),
                    PaymentContent = $"Thanh toán VNPay thành công. Mã giao dịch: {vnpayTranId}",
                    IsActive = true
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                var cart = await _db.Carts
                    .Include(c => c.CartDetails)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart != null)
                {
                    // Lấy danh sách variantId từ đơn hàng
                    var orderVariantIds = items.Select(item => item.GetProperty("VariantId").GetInt32()).ToList();

                    // Xóa các item tương ứng khỏi giỏ hàng
                    var cartItemsToRemove = cart.CartDetails
                        .Where(cd => orderVariantIds.Contains((int)cd.ProductVariantId))
                        .ToList();

                    _db.CartDetails.RemoveRange(cartItemsToRemove);
                    await _db.SaveChangesAsync();
                }

                _logger.LogInformation("Order and Payment saved successfully. OrderId: {orderId}, PaymentId: {paymentId}",
                    order.OrderId, payment.PaymentId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to save order and payment");
                throw;
            }
        }

        // IP client (có xét X-Forwarded-For nếu deploy reverse proxy)
        private static string GetClientIp(HttpContext context)
        {
            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ip)) return ip.Split(',')[0].Trim();

            var rip = context.Connection.RemoteIpAddress;
            if (rip == null) return "127.0.0.1";
            if (rip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                var ipv4 = Dns.GetHostEntry(rip).AddressList
                    .FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                return ipv4?.ToString() ?? "127.0.0.1";
            }
            return rip.ToString();
        }
    }

    public class CheckoutPaymentRequest
    {
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string FullAddress { get; set; }
        public string OrderNote { get; set; }
        public List<CheckoutItem> Items { get; set; }
    }

    public class CheckoutItem
    {
        public int VariantId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
