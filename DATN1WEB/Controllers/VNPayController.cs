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
using DATN1API.Models.Pay;

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
        public async Task<IActionResult> CreatePaymentFromCart(string? orderNote = null, string? orderType = "other", string? promoCode = null)
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

                var itemsSubtotal = cart.CartDetails.Sum(cd => (cd.ProductVariant.SalePrice ?? 0m) * (cd.Quantity ?? 1));
                var discountAmount = 0m;
                var shippingFee = 0m;
                string? validatedPromoCode = null;
                string? promotionType = null;

                if (!string.IsNullOrWhiteSpace(promoCode))
                {
                    var now = DateTime.Now;
                    var promo = await _db.Promotions
                        .FirstOrDefaultAsync(p => p.PromoNameCode == promoCode || p.PromoName == promoCode);

                    if (promo != null)
                    {
                        var validDate = promo.StartDate <= now && (promo.EndDate == null || promo.EndDate >= now);
                        var notExceeded = !promo.Quantity.HasValue || (promo.UsedQuantity ?? 0) < promo.Quantity.Value;

                        if (validDate && notExceeded)
                        {
                            validatedPromoCode = promoCode;

                            if (promo.PromoType?.ToLowerInvariant() == "phần trăm")
                            {
                                promotionType = "percentage";
                                var pct = (decimal)(promo.DiscountValue ?? 0m);
                                discountAmount = Math.Min(itemsSubtotal * pct / 100m, itemsSubtotal);
                            }
                            else if (promo.PromoType?.ToLowerInvariant() == "số tiền cố định")
                            {
                                promotionType = "amount";
                                discountAmount = Math.Min((decimal)(promo.DiscountValue ?? 0m), itemsSubtotal);
                            }
                            else if (promo.PromoType?.ToLowerInvariant() == "miễn phí vận chuyển")
                            {
                                promotionType = "free_shipping";
                                shippingFee = 0m;
                                discountAmount = 0m;
                            }

                            _logger.LogInformation("Applied promotion: {promoCode}, type: {type}, discount: {discount}",
                                promoCode, promotionType, discountAmount);
                        }
                        else
                        {
                            _logger.LogWarning("Invalid promotion: {promoCode}, validDate: {validDate}, notExceeded: {notExceeded}",
                                promoCode, validDate, notExceeded);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Promotion not found: {promoCode}", promoCode);
                    }
                }

                var finalTotal = itemsSubtotal - discountAmount + shippingFee;
                if (finalTotal < 0m) finalTotal = 0m;

                var amountVnd = (int)decimal.Round(finalTotal, 0, MidpointRounding.AwayFromZero);
                var orderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var appUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                var cartItems = cart.CartDetails.Select(cd => new
                {
                    VariantId = cd.ProductVariantId,
                    Quantity = cd.Quantity ?? 1,
                    Price = cd.ProductVariant.SalePrice ?? 0m
                }).ToList();

                var orderData = new
                {
                    UserId = userId,
                    CustomerName = appUser?.FullName ?? "Khách hàng",
                    CustomerPhone = appUser?.PhoneNumber ?? "",
                    FullAddress = appUser?.Address ?? "",
                    Items = cartItems,
                    ItemsSubtotal = itemsSubtotal,
                    DiscountAmount = discountAmount,
                    ShippingFee = shippingFee,
                    Total = finalTotal,
                    OrderNote = orderNote,
                    OrderId = orderId,
                    PromoCode = validatedPromoCode,
                    PromotionType = promotionType
                };

                HttpContext.Session.SetString($"PendingOrder_{orderId}", JsonSerializer.Serialize(orderData));

                _logger.LogInformation("CreatePaymentFromCart: userId={userId}, items={count}, subtotal={subtotal}, discount={discount}, shipping={shipping}, total={totalVnd}, orderId={orderId}, promoCode={promoCode}",
                    userId, cart.CartDetails.Count, itemsSubtotal, discountAmount, shippingFee, amountVnd, orderId, validatedPromoCode);

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
                vnp.AddRequestData("vnp_OrderType", string.IsNullOrWhiteSpace(orderType) ? "other" : orderType);
                vnp.AddRequestData("vnp_ReturnUrl", _vnp_ReturnUrl);
                vnp.AddRequestData("vnp_TxnRef", orderId.ToString());
                vnp.AddRequestData("vnp_ExpireDate", DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"));

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
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });

                if (request.Items == null || !request.Items.Any())
                    return Json(new { success = false, message = "Không có sản phẩm được chọn." });

                if (string.IsNullOrWhiteSpace(request.CustomerName) ||
                    string.IsNullOrWhiteSpace(request.CustomerPhone) ||
                    string.IsNullOrWhiteSpace(request.FullAddress))
                    return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin giao hàng." });

                // ===== TÍNH TIỀN CƠ BẢN =====
                var itemsSubtotal = request.Items.Sum(item => item.Price * item.Quantity);

                decimal discountAmount = Math.Max(0m, request.DiscountAmount ?? 0m);
                if (discountAmount > itemsSubtotal) discountAmount = itemsSubtotal;

                decimal shippingFee = Math.Max(0m, request.ShippingFee ?? 0m);

                // ===== ÁP KHUYẾN MÃI THEO ID (int?) =====
                Promotion? promotion = null;
                if (request.PromoCode.HasValue)
                {
                    int promoId = request.PromoCode.Value;
                    promotion = await _db.Promotions.FirstOrDefaultAsync(p => p.PromoCode == promoId);

                    if (promotion != null)
                    {
                        var now = DateTime.Now;
                        bool validDate = promotion.StartDate <= now && (promotion.EndDate == null || promotion.EndDate >= now);
                        bool notExceeded = !promotion.Quantity.HasValue || (promotion.UsedQuantity ?? 0) < promotion.Quantity.Value;
                        bool minOk = (promotion.MinOrderAmount ?? 0m) <= itemsSubtotal;

                        if (!validDate || !notExceeded || !minOk)
                        {
                            // Không đạt điều kiện → bỏ mã
                            promotion = null;
                            discountAmount = 0m; // reset phần giảm từ client
                        }
                        else
                        {
                            var dbType = (promotion.PromoType ?? string.Empty).Trim().ToLowerInvariant();
                            if (dbType == "phần trăm")
                            {
                                var pct = (decimal)(promotion.DiscountValue ?? 0m);
                                discountAmount = Math.Clamp(itemsSubtotal * pct / 100m, 0m, itemsSubtotal);
                            }
                            else if (dbType == "số tiền cố định")
                            {
                                var val = Math.Max(0m, promotion.DiscountValue ?? 0m);
                                discountAmount = Math.Min(val, itemsSubtotal);
                            }
                            else if (dbType == "miễn phí vận chuyển")
                            {
                                // Freeship: không trừ vào hàng, set ship = 0
                                discountAmount = 0m;
                                shippingFee = 0m;
                            }
                        }
                    }
                }

                // ===== TỔNG CUỐI =====
                var finalTotal = itemsSubtotal - discountAmount + shippingFee;
                if (finalTotal < 0m) finalTotal = 0m;

                // Làm tròn về VND và đổi sang đơn vị VNPay (×100, dạng số nguyên)
                long amountVnd = (long)decimal.Round(finalTotal, 0, MidpointRounding.AwayFromZero);

                var orderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Lưu vào session: PromoCode là int? (FK) + PromoDisplay (mã hiển thị)
                var orderData = new
                {
                    UserId = userId,
                    CustomerName = request.CustomerName,
                    CustomerPhone = request.CustomerPhone,
                    FullAddress = request.FullAddress,
                    Items = request.Items,
                    ItemsSubtotal = itemsSubtotal,
                    DiscountAmount = discountAmount,
                    ShippingFee = shippingFee,
                    Total = finalTotal,
                    OrderNote = request.OrderNote,
                    OrderId = orderId,

                    // Lưu ID khuyến mại để ghi Order.PromoCode (int?) về sau
                    PromoCode = promotion?.PromoCode,                               // int?
                    PromoDisplay = promotion?.PromoNameCode ?? promotion?.PromoName // chỉ để log/hiển thị
                };

                HttpContext.Session.SetString($"PendingOrder_{orderId}", JsonSerializer.Serialize(orderData));

                _logger.LogInformation(
                    "CreatePaymentFromCheckout(int promo): userId={userId}, items={count}, subtotal={subtotal}, discount={discount}, shipping={shipping}, total={total}, orderId={orderId}, promoId={promoId}, promoCodeStr={promoStr}",
                    userId, request.Items.Count, itemsSubtotal, discountAmount, shippingFee, finalTotal, orderId,
                    promotion?.PromoCode, promotion?.PromoNameCode ?? promotion?.PromoName
                );

                // ===== TẠO URL VNPAY (vnp_Amount phải là số nguyên ×100, KHÔNG có . hoặc ,) =====
                var vnp = new VnPayLibrary();
                vnp.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
                vnp.AddRequestData("vnp_Command", "pay");
                vnp.AddRequestData("vnp_TmnCode", _vnp_TmnCode);
                vnp.AddRequestData("vnp_Amount", (amountVnd * 100).ToString()); // ví dụ: 186500000
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

                return Json(new { success = true, paymentUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreatePaymentFromCheckout failed.");
                return Json(new { success = false, message = "Không thể tạo thanh toán. Vui lòng thử lại." });
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
            var bankCode = HttpContext.Request.Query["vnp_BankCode"].ToString();
            var amount = Convert.ToInt64(vnp.GetResponseData("vnp_Amount")) / 100;

            // (optional) vnp_PayDate (yyyyMMddHHmmss)
            DateTime? paidAt = null;
            var payDateStr = vnp.GetResponseData("vnp_PayDate");
            if (!string.IsNullOrWhiteSpace(payDateStr) && DateTime.TryParseExact(payDateStr, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var t))
                paidAt = t;

            // Validate chữ ký
            bool checkSignature = vnp.ValidateSignature(vnp_SecureHash, _vnp_HashSecret);
            if (!checkSignature)
            {
                var invalidSigModel = new PaymentResultViewModel
                {
                    Success = false,
                    OrderId = orderId,
                    TransactionId = vnpayTranId,
                    Amount = amount,
                    BankCode = bankCode,
                    PaidAt = paidAt,
                    ResponseCode = "97",
                    Message = "Chữ ký VNPay không hợp lệ."
                };
                return View("VnPayResult", invalidSigModel);
            }

            if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
            {
                // Lấy dữ liệu đơn đã lưu tạm để ghi DB:
                var sessionKey = $"PendingOrder_{orderId}";
                var orderDataJson = HttpContext.Session.GetString(sessionKey);

                if (!string.IsNullOrEmpty(orderDataJson))
                {
                    var orderData = JsonSerializer.Deserialize<JsonElement>(orderDataJson);
                    await SaveOrderAndPaymentWithPromotion(orderData, vnpayTranId, amount);
                    HttpContext.Session.Remove(sessionKey);
                }

                var okModel = new PaymentResultViewModel
                {
                    Success = true,
                    OrderId = orderId,
                    TransactionId = vnpayTranId,
                    Amount = amount,
                    BankCode = bankCode,
                    PaidAt = paidAt,
                    ResponseCode = vnp_ResponseCode,
                    Message = "Thanh toán VNPay thành công."
                };
                return View("VnPayResult", okModel);
            }
            else
            {
                var failModel = new PaymentResultViewModel
                {
                    Success = false,
                    OrderId = orderId,
                    TransactionId = vnpayTranId,
                    Amount = amount,
                    BankCode = bankCode,
                    PaidAt = paidAt,
                    ResponseCode = vnp_ResponseCode,
                    Message = "Thanh toán VNPay không thành công."
                };
                return View("VnPayResult", failModel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VnPayReturn failed.");
            var errModel = new PaymentResultViewModel
            {
                Success = false,
                Message = "Có lỗi xảy ra khi xử lý kết quả thanh toán.",
                ResponseCode = "99"
            };
            return View("VnPayResult", errModel);
        }
    }

    [HttpGet]
    public async Task<IActionResult> PaymentReturn()
    {
        try
        {
            var vnp = new VnPayLibrary();
            foreach (var (key, value) in Request.Query)
            {
                if (key.StartsWith("vnp_") && !string.IsNullOrEmpty(value))
                    vnp.AddResponseData(key, value!);
            }

            var rawQuery = HttpContext.Request.QueryString.Value ?? string.Empty;
            var vnp_SecureHash = Request.Query["vnp_SecureHash"].ToString();
            var bankCode = Request.Query["vnp_BankCode"].ToString();

            if (string.IsNullOrEmpty(vnp_SecureHash))
            {
                var noSig = new PaymentResultViewModel
                {
                    Success = false,
                    Message = "Thiếu chữ ký xác thực từ VNPay.",
                    ResponseCode = "97"
                };
                return View("VnPayResult", noSig);
            }

            var valid = VnPayLibrary.ValidateSignatureFromRaw(
                rawQuery, vnp_SecureHash, _vnp_HashSecret,
                out var rawForHash, out var myHashUpper);

            var orderId = Convert.ToInt64(vnp.GetResponseData("vnp_TxnRef"));
            var vnpayTranId = Convert.ToInt64(vnp.GetResponseData("vnp_TransactionNo"));
            var responseCode = vnp.GetResponseData("vnp_ResponseCode");
            var txnStatus = vnp.GetResponseData("vnp_TransactionStatus");
            var amount = Convert.ToInt64(vnp.GetResponseData("vnp_Amount")) / 100;

            // vnp_PayDate
            DateTime? paidAt = null;
            var payDateStr = vnp.GetResponseData("vnp_PayDate");
            if (!string.IsNullOrWhiteSpace(payDateStr) && DateTime.TryParseExact(payDateStr, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var t))
                paidAt = t;

            if (!valid)
            {
                var invalidModel = new PaymentResultViewModel
                {
                    Success = false,
                    OrderId = orderId,
                    TransactionId = vnpayTranId,
                    Amount = amount,
                    BankCode = bankCode,
                    PaidAt = paidAt,
                    Message = "Chữ ký VNPay không hợp lệ.",
                    ResponseCode = "97"
                };
                return View("VnPayResult", invalidModel);
            }

            if (responseCode == "00" && txnStatus == "00")
            {
                var sessionKey = $"PendingOrder_{orderId}";
                var orderDataJson = HttpContext.Session.GetString(sessionKey);

                if (!string.IsNullOrEmpty(orderDataJson))
                {
                    var orderData = JsonSerializer.Deserialize<JsonElement>(orderDataJson);
                    await SaveOrderAndPaymentWithPromotion(orderData, vnpayTranId, amount);
                    HttpContext.Session.Remove(sessionKey);
                }

                var okModel = new PaymentResultViewModel
                {
                    Success = true,
                    OrderId = orderId,
                    TransactionId = vnpayTranId,
                    Amount = amount,
                    BankCode = bankCode,
                    PaidAt = paidAt,
                    Message = "Thanh toán VNPay thành công.",
                    ResponseCode = responseCode
                };
                return View("VnPayResult", okModel);
            }
            else
            {
                var failModel = new PaymentResultViewModel
                {
                    Success = false,
                    OrderId = orderId,
                    TransactionId = vnpayTranId,
                    Amount = amount,
                    BankCode = bankCode,
                    PaidAt = paidAt,
                    Message = $"Thanh toán không thành công (mã: {responseCode}).",
                    ResponseCode = responseCode
                };
                return View("VnPayResult", failModel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PaymentReturn failed.");
            var errModel = new PaymentResultViewModel
            {
                Success = false,
                Message = "Có lỗi xảy ra khi xử lý kết quả thanh toán.",
                ResponseCode = "99"
            };
            return View("VnPayResult", errModel);
        }
    }


    private async Task SaveOrderAndPaymentWithPromotion(JsonElement orderData, long vnpayTranId, long amount)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var userId = orderData.GetProperty("UserId").GetString();

                var itemsSubtotal = orderData.TryGetProperty("ItemsSubtotal", out var subtotalProp) ? subtotalProp.GetDecimal() : 0m;
                var discountAmount = orderData.TryGetProperty("DiscountAmount", out var discountProp) ? discountProp.GetDecimal() : 0m;
                var shippingFee = orderData.TryGetProperty("ShippingFee", out var shippingProp) ? shippingProp.GetDecimal() : 0m;

                // ĐỌC PromoCode (int?)
                int? promoId = null;
                if (orderData.TryGetProperty("PromoCode", out var promoIdProp))
                {
                    if (promoIdProp.ValueKind == JsonValueKind.Number && promoIdProp.TryGetInt32(out var tmp))
                        promoId = tmp;
                }

                // Nạp promotion nếu có
                Promotion? promoEntity = null;
                if (promoId.HasValue)
                {
                    promoEntity = await _db.Promotions.FirstOrDefaultAsync(p => p.PromoCode == promoId.Value);
                }

                // Tạo Order — LƯU ID vào Order.PromoCode
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
                    Note = orderData.TryGetProperty("OrderNote", out var noteProperty) ? noteProperty.GetString() : null,

                    PromoCode = promoEntity?.PromoCode,        // <-- FK int?
                    PromotionPromoCode = promoEntity?.PromoCode,
                    ShippingFee = shippingFee,
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // OrderDetails
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

                // Payment content (hiển thị mã người dùng quen)
                var paymentContent = $"Thanh toán VNPay thành công. Mã giao dịch: {vnpayTranId}";
                if (promoEntity != null)
                {
                    var promoDisplay = promoEntity.PromoNameCode ?? promoEntity.PromoName;
                    paymentContent += $". Mã khuyến mại: {promoDisplay}, Giảm giá: {discountAmount:N0} VNĐ";
                }
                if (shippingFee > 0)
                {
                    paymentContent += $". Phí vận chuyển: {shippingFee:N0} VNĐ";
                }

                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    MethodName = "VNPay",
                    PaymentDate = DateTime.Now,
                    Amount = amount,
                    PaymentStatus = "Thành công",
                    BankTransactionCode = vnpayTranId.ToString(),
                    PaymentContent = paymentContent,
                    IsActive = true
                };
                _db.Payments.Add(payment);

                // Tăng UsedQuantity nếu có mã
                if (promoEntity != null)
                {
                    promoEntity.UsedQuantity = (promoEntity.UsedQuantity ?? 0) + 1;
                    _db.Promotions.Update(promoEntity);

                    HttpContext.Session.SetString("LastUsedPromotion", JsonSerializer.Serialize(new
                    {
                        PromoId = promoEntity.PromoCode,
                        PromoCode = promoEntity.PromoNameCode ?? promoEntity.PromoName,
                        NewUsedCount = promoEntity.UsedQuantity,
                        TotalCount = promoEntity.Quantity
                    }));
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Xoá cart items
                var cart = await _db.Carts.Include(c => c.CartDetails).FirstOrDefaultAsync(c => c.UserId == userId);
                if (cart != null)
                {
                    var variantIds = items.Select(x => x.GetProperty("VariantId").GetInt32()).ToList();
                    var toRemove = cart.CartDetails.Where(cd => variantIds.Contains((int)cd.ProductVariantId)).ToList();
                    _db.CartDetails.RemoveRange(toRemove);
                    await _db.SaveChangesAsync();
                }

                _logger.LogInformation("Order + Payment saved. OrderId={orderId}, PaymentId={paymentId}, PromoCode={promoId}, Discount={discount}, Shipping={shipping}",
                    order.OrderId, payment.PaymentId, promoId, discountAmount, shippingFee);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to save order and payment with promotion data");
                throw;
            }
        }


        private async Task SaveOrderAndPayment(JsonElement orderData, long vnpayTranId, long amount)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var userId = orderData.GetProperty("UserId").GetString();

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
                    var orderVariantIds = items.Select(item => item.GetProperty("VariantId").GetInt32()).ToList();
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
}
