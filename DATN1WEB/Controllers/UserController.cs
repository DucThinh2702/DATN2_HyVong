using DATN1API.Data;
using DATN1API.Models;
using DATN1API.Helpers;
using DATN1WEB.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.Net.Http;
using System.Net.Http.Json;   // <= cần cho GetFromJsonAsync
using System.Security.Claims;
using System.Text.RegularExpressions;

public class UserController : Controller
{
    // ===== FIELDS =====
    private readonly HttpClient _client;
    private readonly VnPayService _vnpayService;
    private readonly PayOSService _payOSService;
    private readonly DatnContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    // ===== CONSTRUCTOR (duy nhất) =====
    public UserController(
        IHttpClientFactory httpClientFactory,
        DatnContext context,
        PayOSService payOSService,
        VnPayService vnpayService,
        UserManager<ApplicationUser> userManager)
    {
        _client = httpClientFactory.CreateClient("api");
        _context = context;
        _payOSService = payOSService;
        _vnpayService = vnpayService;
        _userManager = userManager;
    }

    // ================== ACTIONS ==================

    // Index có lọc & phân trang qua API (một bản duy nhất)
    public async Task<IActionResult> Index(int? categoryId, int page = 1)
    {
        if (User.Identity.IsAuthenticated) ViewBag.UserName = User.Identity.Name;

        var products = await _client.GetFromJsonAsync<List<Product>>("api/Product") ?? new();
        var categories = await _client.GetFromJsonAsync<List<Category>>("api/Categories") ?? new();

        ViewBag.Categories = categories;
        ViewBag.SelectedCategoryId = categoryId;

        if (categoryId != null)
            products = products.Where(p => p.CategoryId == categoryId).ToList();

        const int pageSize = 8;
        int totalProducts = products.Count;
        var pagedProducts = products.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalProducts / pageSize);
        ViewBag.Top4Products = products.OrderByDescending(p => p.CreatedDate).Take(4).ToList();

        return View(pagedProducts);
    }

    // Chọn biến thể
    public IActionResult ChonBienThe(int id)
    {
        if (User.Identity.IsAuthenticated) ViewBag.UserName = User.Identity.Name;

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

    // Thêm vào giỏ
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

    // Mua ngay
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

    // Xoá khỏi giỏ
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

    // GIỎ HÀNG (bản duy nhất – giữ bản đầy đủ, KHÔNG tạo thêm GioHang() khác)
    public IActionResult GioHang()
    {
        if (User.Identity.IsAuthenticated)
            ViewBag.UserName = User.Identity.Name;

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        var cart = _context.Carts
            .Include(c => c.CartDetails).ThenInclude(cd => cd.ProductVariant).ThenInclude(pv => pv.Product)
            .Include(c => c.CartDetails).ThenInclude(cd => cd.ProductVariant).ThenInclude(pv => pv.Color)
            .Include(c => c.CartDetails).ThenInclude(cd => cd.ProductVariant).ThenInclude(pv => pv.Size)
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

    // Thanh toán PayOS
    [HttpPost]
    public async Task<IActionResult> ThanhToanQuaPayOS()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        var cart = _context.Carts
            .Include(c => c.CartDetails).ThenInclude(cd => cd.ProductVariant).ThenInclude(pv => pv.Product)
            .FirstOrDefault(c => c.UserId == userId);

        if (cart == null || !cart.CartDetails.Any())
        {
            TempData["Message"] = "Giỏ hàng rỗng.";
            return RedirectToAction("GioHang");
        }

        decimal total = cart.CartDetails.Sum(cd => (cd.ProductVariant.SalePrice ?? 0) * (cd.Quantity ?? 1));
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string description = Uri.EscapeDataString("Thanh toán đơn hàng túi xách");
        string returnUrl = "https://4e38f8661e44.ngrok-free.app/User/ThanhToanThanhCong";
        string cancelUrl = "https://4e38f8661e44.ngrok-free.app/User/GioHang";

        var items = cart.CartDetails.Select(cd => new PayOSItem
        {
            name = Uri.EscapeDataString(cd.ProductVariant.Product?.ProductName ?? "Sản phẩm"),
            quantity = cd.Quantity ?? 1,
            price = cd.ProductVariant.SalePrice.HasValue ? Convert.ToInt32(cd.ProductVariant.SalePrice.Value) : 0
        }).ToList();

        var qrCodeUrl = await _payOSService.CreatePaymentRequestAsync(total, orderCode, description, returnUrl, cancelUrl, items);

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
                        var errorObj = JsonConvert.DeserializeObject<dynamic>(raw);
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

    // Kết quả PayOS
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
            var cart = _context.Carts
                .Include(c => c.CartDetails).ThenInclude(cd => cd.ProductVariant)
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null || !cart.CartDetails.Any())
            {
                ViewBag.Message = "Không tìm thấy giỏ hàng để tạo đơn.";
                return View("KetQuaThanhToan");
            }

            var totalAmount = cart.CartDetails.Sum(cd => (cd.ProductVariant.SalePrice ?? 0) * cd.Quantity);
            var totalQuantity = cart.CartDetails.Sum(cd => cd.Quantity);

            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                TotalAmount = totalAmount,
                Quantity = totalQuantity,
                OrderStatus = "Chờ xác nhận",
                PaymentStatus = "PAID",
                OrderDetails = new List<OrderDetail>()
            };

            foreach (var item in cart.CartDetails)
            {
                order.OrderDetails.Add(new OrderDetail
                {
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity,
                    UnitPrice = item.ProductVariant.SalePrice ?? 0,
                    TotalPrice = (item.ProductVariant.SalePrice ?? 0) * item.Quantity
                });
            }

            _context.Orders.Add(order);
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

    // ===== CÁC TRANG TĨNH: MỖI ACTION CHỈ 1 LẦN =====
    [Authorize]
    public IActionResult ThanhToan()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        ViewBag.UserId = userId;   // truyền xuống view
        return View();
    }
    public IActionResult LienHe() => View();
    public IActionResult QuenMatKhau() => View();
    public IActionResult DangNhap() => View();
    public IActionResult DangKy() => View();
    public IActionResult ChiTiet() => View();

    // ===== HỒ SƠ NGƯỜI DÙNG =====
    [Authorize]
    public async Task<IActionResult> ProfileUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("DangNhap");

        var userProfile = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
        if (userProfile == null)
        {
            userProfile = new ApplicationUser { Email = user.Email, UserName = user.UserName };
        }
        return View(userProfile);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfileUser(ApplicationUser model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("DangNhap");

        if (string.IsNullOrWhiteSpace(model.FullName))
            ModelState.AddModelError("FullName", "Họ và tên là bắt buộc.");
        else if (model.FullName.Length < 2 || model.FullName.Length > 100)
            ModelState.AddModelError("FullName", "Họ và tên phải từ 2 đến 100 ký tự.");

        if (string.IsNullOrWhiteSpace(model.UserName))
            ModelState.AddModelError("UserName", "Tên đăng nhập là bắt buộc.");
        else if (model.UserName.Length < 3 || model.UserName.Length > 50)
            ModelState.AddModelError("UserName", "Tên đăng nhập phải từ 3 đến 50 ký tự.");

        if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
        {
            var phoneRegex = new Regex(@"^(0[3|5|7|8|9])+([0-9]{8})$");
            if (!phoneRegex.IsMatch(model.PhoneNumber))
                ModelState.AddModelError("PhoneNumber", "Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam.");
            else
            {
                var existingPhone = await _context.Users
                    .AnyAsync(u => u.PhoneNumber == model.PhoneNumber && u.Email != user.Email);
                if (existingPhone)
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại này đã được sử dụng.");
            }
        }

        if (model.BirthDate.HasValue)
        {
            var age = DateTime.Now.Year - model.BirthDate.Value.Year;
            if (model.BirthDate.Value.Date > DateTime.Now.AddYears(-age)) age--;
            if (age < 15) ModelState.AddModelError("DateOfBirth", "Bạn phải từ 13 tuổi trở lên.");
            else if (age > 120) ModelState.AddModelError("DateOfBirth", "Ngày sinh không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(model.Address) && model.Address.Length > 200)
            ModelState.AddModelError("Address", "Địa chỉ không được vượt quá 200 ký tự.");

        if (!string.IsNullOrWhiteSpace(model.Gender) && !new[] { "Nam", "Nữ", "Khác" }.Contains(model.Gender))
            ModelState.AddModelError("Gender", "Giới tính không hợp lệ.");

        if (!ModelState.IsValid) return View(model);

        try
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (existingUser != null)
            {
                existingUser.FullName = model.FullName?.Trim();
                existingUser.UserName = model.UserName?.Trim();
                existingUser.PhoneNumber = model.PhoneNumber?.Trim();
                existingUser.Address = model.Address?.Trim();
                existingUser.BirthDate = model.BirthDate;
                existingUser.Gender = model.Gender?.Trim();
                existingUser.UpdatedAt = DateTime.Now;

                _context.Users.Update(existingUser);
            }
            else
            {
                var newUser = new ApplicationUser
                {
                    FullName = model.FullName?.Trim(),
                    UserName = model.UserName?.Trim(),
                    Email = user.Email,
                    PhoneNumber = model.PhoneNumber?.Trim(),
                    Address = model.Address?.Trim(),
                    BirthDate = model.BirthDate,
                    Gender = model.Gender?.Trim(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.Users.Add(newUser);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công!";
            return RedirectToAction("ProfileUser");
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Có lỗi xảy ra khi lưu thông tin. Vui lòng kiểm tra lại dữ liệu.");
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật thông tin: " + ex.Message;
            return View(model);
        }
    }
}
