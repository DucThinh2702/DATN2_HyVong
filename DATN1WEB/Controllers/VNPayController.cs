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

                // ====== TÍNH TIỀN ======
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

                            var type = promo.PromoType?.Trim().ToLowerInvariant();
                            if (type == "phần trăm")
                            {
                                promotionType = "percentage";
                                var pct = (decimal)(promo.DiscountValue ?? 0m);
                                discountAmount = Math.Min(itemsSubtotal * pct / 100m, itemsSubtotal);
                            }
                            else if (type == "số tiền cố định")
                            {
                                promotionType = "amount";
                                discountAmount = Math.Min((decimal)(promo.DiscountValue ?? 0m), itemsSubtotal);
                            }
                            else if (type == "miễn phí vận chuyển")
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

                // ====== ✅ KIỂM TRA TỒN TRƯỚC KHI TẠO LINK VNPAY ======
                var lines = cart.CartDetails
                    .GroupBy(cd => cd.ProductVariantId)
                    .Select(g => new { VariantId = (int)g.Key, Qty = g.Sum(x => x.Quantity ?? 1) })
                    .ToList();

                foreach (var l in lines)
                {
                    var v = await _db.ProductVariants.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.VariantId == l.VariantId);
                    if (v == null || (v.Stock ?? 0) < l.Qty)
                    {
                        TempData["Message"] = "Một số sản phẩm không đủ hàng. Vui lòng cập nhật giỏ hàng.";
                        _logger.LogWarning("CreatePaymentFromCart: Out of stock before VNPay. VariantId={vid}, Need={need}, Stock={stock}",
                            l.VariantId, l.Qty, v?.Stock);
                        return RedirectToAction("GioHang", "User");
                    }
                }

                // ====== TẠO URL VNPAY ======
                var vnp = new VnPayLibrary();
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
            // ✅ Kiểm tra tồn trước khi tạo link thanh toán
            var lines = request.Items
                .GroupBy(i => i.VariantId)
                .Select(g => new { VariantId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            foreach (var l in lines)
            {
                var v = await _db.ProductVariants.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.VariantId == l.VariantId);
                if (v == null || (v.Stock ?? 0) < l.Qty)
                {
                    return Json(new { success = false, message = "Một số sản phẩm không đủ hàng. Vui lòng cập nhật giỏ hàng." });
                }
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
            // ===== Idempotency: đã ghi nhận giao dịch này trước đó? =====
            var tranCode = vnpayTranId.ToString();
            var existedPayment = await _db.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.MethodName == "VNPay" &&
                    p.BankTransactionCode == tranCode &&
                    p.PaymentStatus == "Thành công");

            if (existedPayment != null)
            {
                _logger.LogWarning("Skip duplicate VNPay callback. BankTransactionCode={tran}", tranCode);
                return; // đã xử lý rồi
            }

            // ===== Parse items một lần =====
            var itemsList = orderData.GetProperty("Items")
                .EnumerateArray()
                .Select(it => new
                {
                    VariantId = it.GetProperty("VariantId").GetInt32(),
                    Quantity = it.GetProperty("Quantity").GetInt32(),
                    Price = it.GetProperty("Price").GetDecimal()
                })
                .ToList();

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var userId = orderData.GetProperty("UserId").GetString();

                var itemsSubtotal = orderData.TryGetProperty("ItemsSubtotal", out var subtotalProp) ? subtotalProp.GetDecimal() : 0m;
                var discountAmount = orderData.TryGetProperty("DiscountAmount", out var discountProp) ? discountProp.GetDecimal() : 0m;
                var shippingFee = orderData.TryGetProperty("ShippingFee", out var shippingProp) ? shippingProp.GetDecimal() : 0m;

                // ===== ĐỌC PromoCode (int?) & nạp entity =====
                int? promoId = null;
                if (orderData.TryGetProperty("PromoCode", out var promoIdProp) &&
                    promoIdProp.ValueKind == JsonValueKind.Number &&
                    promoIdProp.TryGetInt32(out var tmp))
                {
                    promoId = tmp;
                }

                Promotion? promoEntity = null;
                if (promoId.HasValue)
                    promoEntity = await _db.Promotions.FirstOrDefaultAsync(p => p.PromoCode == promoId.Value);

                // ===== 1) TRỪ TỒN KHO — nguyên tử theo từng biến thể =====
                var grouped = itemsList
                    .GroupBy(x => x.VariantId)
                    .Select(g => new { VariantId = g.Key, Qty = g.Sum(i => i.Quantity) })
                    .ToList();

                foreach (var g in grouped)
                {
                    var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ProductVariants
                SET Stock = Stock - {g.Qty}
                WHERE VariantId = {g.VariantId}
                  AND COALESCE(Stock,0) >= {g.Qty};
            ");

                    if (affected == 0)
                    {
                        // Lấy label đẹp để báo lỗi/log
                        var pv = await _db.ProductVariants
                            .Include(v => v.Product).Include(v => v.Color).Include(v => v.Size)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(v => v.VariantId == g.VariantId);

                        var name = pv?.Product?.ProductName ?? $"Biến thể #{g.VariantId}";
                        var color = pv?.Color?.ColorName;
                        var size = pv?.Size?.SizeName;
                        var label = (color == null && size == null) ? name
                                   : $"{name} ({color}{(color != null && size != null ? " / " : "")}{size})";

                        _logger.LogError("Out of stock during VNPay capture. VariantId={vid}, Need={need}", g.VariantId, g.Qty);
                        throw new InvalidOperationException($"{label} không đủ hàng trong kho.");
                    }
                }

                // ===== 2) TẠO ORDER (đã thanh toán → Chờ nhận hàng) =====
                var order = new Order
                {
                    UserId = userId,
                    RecipientName = orderData.GetProperty("CustomerName").GetString(),
                    RecipientPhone = orderData.GetProperty("CustomerPhone").GetString(),
                    DeliveryAddress = orderData.GetProperty("FullAddress").GetString(),
                    OrderDate = DateTime.Now,
                    TotalAmount = orderData.GetProperty("Total").GetDecimal(),
                    OrderStatus = "Chờ nhận hàng",   // đã thanh toán
                    PaymentStatus = "Đã thanh toán",
                    Note = orderData.TryGetProperty("OrderNote", out var noteProperty) ? noteProperty.GetString() : null,

                    PromoCode = promoEntity?.PromoCode, // FK int?
                    PromotionPromoCode = promoEntity?.PromoCode,
                    ShippingFee = shippingFee,
                    Quantity = itemsList.Sum(i => i.Quantity)
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // ===== 3) ORDER DETAILS =====
                foreach (var it in itemsList)
                {
                    _db.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.OrderId,
                        ProductVariantId = it.VariantId,
                        Quantity = it.Quantity,
                        UnitPrice = it.Price,
                        TotalPrice = it.Price * it.Quantity
                    });
                }

                // ===== 4) PAYMENT =====
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
                    BankTransactionCode = tranCode,
                    PaymentContent = paymentContent,
                    IsActive = true
                };
                _db.Payments.Add(payment);

                // ===== 5) Cập nhật dùng mã khuyến mại =====
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

                // ===== 6) XOÁ GIỎ HÀNG (ngoài transaction chính, như logic hiện tại) =====
                var cart = await _db.Carts
                    .Include(c => c.CartDetails)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart != null)
                {
                    var variantIds = itemsList.Select(x => x.VariantId).ToList();
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
                _logger.LogError(ex, "Failed to save order and payment with promotion data (VNPay)");
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


        private async Task<(bool ok, string? msg)> ConsumeStockAsync(IEnumerable<(int VariantId, int Qty)> lines)
        {
            var grouped = lines.GroupBy(x => x.VariantId).Select(g => new { VariantId = g.Key, Qty = g.Sum(i => i.Qty) });
            foreach (var item in grouped)
            {
                var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ProductVariants
            SET Stock = Stock - {item.Qty}
            WHERE VariantId = {item.VariantId}
              AND COALESCE(Stock,0) >= {item.Qty};
        ");
                if (affected == 0)
                {
                    var pv = await _db.ProductVariants
                        .Include(v => v.Product).Include(v => v.Color).Include(v => v.Size)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.VariantId == item.VariantId);

                    var name = pv?.Product?.ProductName ?? $"Biến thể #{item.VariantId}";
                    var color = pv?.Color?.ColorName;
                    var size = pv?.Size?.SizeName;
                    var label = (color == null && size == null) ? name
                                : $"{name} ({color}{(color != null && size != null ? " / " : "")}{size})";

                    return (false, $"{label} không đủ hàng trong kho.");
                }
            }
            return (true, null);
        }

        private async Task RestockAsync(IEnumerable<(int VariantId, int Qty)> lines)
        {
            var grouped = lines.GroupBy(x => x.VariantId).Select(g => new { VariantId = g.Key, Qty = g.Sum(i => i.Qty) });
            foreach (var item in grouped)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ProductVariants
            SET Stock = COALESCE(Stock,0) + {item.Qty}
            WHERE VariantId = {item.VariantId};
        ");
            }
        }

    }
}
