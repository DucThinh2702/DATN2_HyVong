using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using DATN1API.Helpers;

public class UserController : Controller
{
    private readonly HttpClient _client;
    private readonly DatnContext _context;
    private readonly VnPayService _vnpayService;

    private readonly PayOSService _payOSService;

    public UserController(IHttpClientFactory httpClientFactory, DatnContext context, PayOSService payOSService, VnPayService vnpayService)
    {
        _client = httpClientFactory.CreateClient("api");
        _context = context;
        _payOSService = payOSService;
        _vnpayService = vnpayService;

    }



    public async Task<IActionResult> Index(int? categoryId, int page = 1)
    {
        if (User.Identity.IsAuthenticated)
        {
            ViewBag.UserName = User.Identity.Name;
        }

        var products = await _client.GetFromJsonAsync<List<Product>>("api/Product") ?? new();
        var categories = await _client.GetFromJsonAsync<List<Category>>("api/Categories") ?? new();

        ViewBag.Categories = categories;
        ViewBag.SelectedCategoryId = categoryId;

        if (categoryId != null)
        {
            products = products.Where(p => p.CategoryId == categoryId).ToList();
        }

        int pageSize = 8;
        int totalProducts = products.Count;
        var pagedProducts = products.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalProducts / pageSize);
        ViewBag.Top4Products = products.OrderByDescending(p => p.CreatedDate).Take(4).ToList();

        return View(pagedProducts);
    }

    public IActionResult ChonBienThe(int id)
    {
        if (User.Identity.IsAuthenticated)
        {
            ViewBag.UserName = User.Identity.Name;
        }

        var product = _context.Products.FirstOrDefault(p => p.ProductId == id);
        if (product == null) return NotFound();

        var variants = _context.ProductVariants
            .Include(v => v.Color)
            .Include(v => v.Size)
            .Where(v => v.ProductId == id && v.Status == "Active")
            .ToList();

        ViewBag.Colors = variants.Select(v => v.Color).Distinct().ToList();
        ViewBag.Sizes = variants.Select(v => v.Size).Distinct().ToList();

        var variantDtos = variants.Select(v => new
        {
            v.VariantId,
            v.ColorId,
            v.SizeId,
            v.SalePrice,
            v.Stock,
            v.ThumbnailImage
        }).ToList();

        ViewBag.VariantsJson = JsonConvert.SerializeObject(variantDtos);
        return View(product);
    }

    [HttpPost]
    public IActionResult ThemVaoGio(int variantId, int quantity)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        var cart = _context.Carts.Include(c => c.CartDetails)
                                 .FirstOrDefault(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now
            };
            _context.Carts.Add(cart);
            _context.SaveChanges();
        }

        var existingItem = cart.CartDetails.FirstOrDefault(c => c.ProductVariantId == variantId);
        if (existingItem != null)
            existingItem.Quantity += quantity;
        else
            _context.CartDetails.Add(new CartDetail
            {
                CartId = cart.CartId,
                ProductVariantId = variantId,
                Quantity = quantity
            });

        cart.LastUpdated = DateTime.Now;
        _context.SaveChanges();

        return RedirectToAction("Index", "User");
    }

    [HttpPost]
    public IActionResult MuaNgay(int variantId, int quantity)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        var variant = _context.ProductVariants.FirstOrDefault(v => v.VariantId == variantId);
        if (variant == null) return NotFound("Biến thể sản phẩm không tồn tại.");

        var cart = _context.Carts
                           .Include(c => c.CartDetails)
                           .FirstOrDefault(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now
            };
            _context.Carts.Add(cart);
            _context.SaveChanges();
        }

        var existingItem = cart.CartDetails.FirstOrDefault(c => c.ProductVariantId == variantId);
        if (existingItem != null)
            existingItem.Quantity += quantity;
        else
            _context.CartDetails.Add(new CartDetail
            {
                CartId = cart.CartId,
                ProductVariantId = variantId,
                Quantity = quantity
            });

        cart.LastUpdated = DateTime.Now;
        _context.SaveChanges();

        return RedirectToAction("GioHang", "User", new { id = cart.CartId });
    }

    [HttpPost]
    public IActionResult XoaKhoiGio(int cartDetailId)
    {
        var detail = _context.CartDetails.FirstOrDefault(x => x.CartDetailId == cartDetailId);
        if (detail != null)
        {
            _context.CartDetails.Remove(detail);
            _context.SaveChanges();
        }
        return RedirectToAction("GioHang");
    }

    public IActionResult GioHang()
    {
        if (User.Identity.IsAuthenticated)
        {
            ViewBag.UserName = User.Identity.Name;
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        var cart = _context.Carts
            .Include(c => c.CartDetails)
                .ThenInclude(cd => cd.ProductVariant)
                    .ThenInclude(pv => pv.Product)
            .Include(c => c.CartDetails)
                .ThenInclude(cd => cd.ProductVariant)
                    .ThenInclude(pv => pv.Color)
            .Include(c => c.CartDetails)
                .ThenInclude(cd => cd.ProductVariant)
                    .ThenInclude(pv => pv.Size)
            .FirstOrDefault(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now
            };
            _context.Carts.Add(cart);
            _context.SaveChanges();
        }

        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> ThanhToanQuaPayOS()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        // Lấy giỏ hàng của user
        var cart = _context.Carts
            .Include(c => c.CartDetails)
                .ThenInclude(cd => cd.ProductVariant)
                    .ThenInclude(pv => pv.Product)
            .FirstOrDefault(c => c.UserId == userId);

        if (cart == null || !cart.CartDetails.Any())
        {
            TempData["Message"] = "Giỏ hàng rỗng.";
            return RedirectToAction("GioHang");
        }

        // Tính tổng tiền của giỏ hàng
        decimal total = cart.CartDetails.Sum(cd => (cd.ProductVariant.SalePrice.HasValue ? cd.ProductVariant.SalePrice.Value : 0) * (cd.Quantity.HasValue ? cd.Quantity.Value : 1));
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string description = "Thanh toán đơn hàng túi xách";
        string returnUrl = "https://4e38f8661e44.ngrok-free.app/User/ThanhToanThanhCong";
        string cancelUrl = "https://4e38f8661e44.ngrok-free.app/User/GioHang";

        // Mã hóa description để đảm bảo không có ký tự lạ
        description = Uri.EscapeDataString(description);

        // Chuẩn bị danh sách items gửi cho PayOS
        var items = cart.CartDetails.Select(cd => new PayOSItem
        {
            name = Uri.EscapeDataString(cd.ProductVariant.Product?.ProductName ?? "Sản phẩm"),  // Đảm bảo tên sản phẩm là string
            quantity = cd.Quantity.HasValue ? cd.Quantity.Value : 1,  // Ép kiểu từ int? sang int
            price = cd.ProductVariant.SalePrice.HasValue ? Convert.ToInt32(cd.ProductVariant.SalePrice.Value) : 0 // Ép kiểu từ decimal? sang int
        }).ToList();

        // Gửi yêu cầu tới PayOS để tạo QR
        var qrCodeUrl = await _payOSService.CreatePaymentRequestAsync(total, orderCode, description, returnUrl, cancelUrl, items);

        // Hiển thị kết quả lên view
        ViewBag.TotalAmount = total;
        ViewBag.OrderCode = orderCode;
        ViewBag.QrCodeUrl = qrCodeUrl;

        if (qrCodeUrl == null)
        {
            if (!string.IsNullOrEmpty(_payOSService.LastError))
            {
                try
                {
                    var raw = _payOSService.LastError.Trim();
                    if (raw.StartsWith("{"))
                    {
                        var errorObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(raw);

                        if (errorObj is JObject)
                        {
                            ViewBag.PayOSErrorMessage = errorObj?.desc ?? "Không rõ lỗi từ PayOS";
                            ViewBag.PayOSErrorDetails = errorObj?.data?.ToString() ?? "";
                        }
                    }
                    else
                    {
                        ViewBag.PayOSErrorMessage = "Phản hồi không phải JSON từ PayOS.";
                        ViewBag.PayOSErrorDetails = raw;
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.PayOSErrorMessage = "Lỗi JSON: " + ex.Message;
                    ViewBag.PayOSErrorDetails = _payOSService.LastError;
                }

                ViewBag.RawError = _payOSService.LastError;
            }
            else
            {
                ViewBag.PayOSErrorMessage = "Không thể tạo mã QR. Lỗi không xác định.";
                ViewBag.PayOSErrorDetails = "";
            }
        }

        return View("HienThiQR");
    }
    public async Task<IActionResult> ThanhToanThanhCong(long orderCode)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        var result = await _payOSService.KiemTraTrangThaiThanhToan(orderCode);

        if (result == null)
        {
            ViewBag.Message = "Không thể kiểm tra trạng thái thanh toán.";
            ViewBag.Error = _payOSService.LastError;
            return View("KetQuaThanhToan");
        }

        var status = result["data"]?["status"]?.ToString();

        if (status == "PAID")
        {
            // Lấy giỏ hàng
            var cart = _context.Carts
                .Include(c => c.CartDetails)
                .ThenInclude(cd => cd.ProductVariant)
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null || !cart.CartDetails.Any())
            {
                ViewBag.Message = "Không tìm thấy giỏ hàng để tạo đơn.";
                return View("KetQuaThanhToan");
            }

            // Tính tổng tiền & số lượng
            var totalAmount = cart.CartDetails.Sum(cd => (cd.ProductVariant.SalePrice ?? 0) * cd.Quantity);
            var totalQuantity = cart.CartDetails.Sum(cd => cd.Quantity);

            // Tạo đơn hàng mới
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                TotalAmount = totalAmount,
                Quantity = totalQuantity,
                OrderStatus = "Chờ xác nhận",
                PaymentStatus = "PAID",
                OrderDetails = new List<OrderDetail>(),
                // Bạn có thể gán thêm các trường này nếu có UI:
                // RecipientName = "...",
                // RecipientPhone = "...",
                // DeliveryAddress = "...",
                // Note = "...",
                // PromoCode = "...",
                // ShippingFee = 0
            };

            foreach (var item in cart.CartDetails)
            {
                var detail = new OrderDetail
                {
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity,
                    UnitPrice = item.ProductVariant.SalePrice ?? 0,
                    TotalPrice = (item.ProductVariant.SalePrice ?? 0) * item.Quantity
                };

                order.OrderDetails.Add(detail);
            }

            _context.Orders.Add(order);

            // Xóa giỏ hàng sau khi đã chuyển thành đơn
            _context.CartDetails.RemoveRange(cart.CartDetails);
            _context.SaveChanges();

            ViewBag.Message = "🎉 Thanh toán thành công và đơn hàng đã được ghi nhận!";
        }
        else
        {
            ViewBag.Message = $"⚠️ Giao dịch chưa hoàn tất. Trạng thái hiện tại: {status}";
        }

        return View("KetQuaThanhToan");
    }
    
    public IActionResult ThanhToan() => View();
    public IActionResult LienHe() => View();
    public IActionResult QuenMatKhau() => View();
    public IActionResult DangNhap() => View();
    public IActionResult DangKy() => View();
    public IActionResult ChiTiet() => View();
}
