using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using DATN1API.Pay;
using Microsoft.AspNetCore.Authorization;
using DATN1API.Models.Pay;
using DATNAPI1.Controllers;
using System.Linq;
using DATN1API.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using DATN1API.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using DATN1WEB.Models;

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

        private readonly DatnContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserController(DatnContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        public IActionResult Index()
        {
            var products = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Where(p => p.Status.ToLower() == "active")
                .ToList();

            return View(products); // truyền danh sách sản phẩm xuống view
        }

        public IActionResult GioHang()
        {
            return View();
        }

        public IActionResult ThanhToan()
        {
            return View();
        }

        public IActionResult LienHe()
        {
            return View();
        }

        public IActionResult QuenMatKhau()
        {
            return View();
        }

        public IActionResult DangNhap()
        {
            return View();
        }

        public IActionResult DangKy()
        {
            return View();
        }

        public IActionResult ChiTiet()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> ProfileUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("DangNhap");
            }

            var userProfile = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email);

            if (userProfile == null)
            {
                // Create a new user profile if doesn't exist
                userProfile = new ApplicationUser
                {
                    Email = user.Email,
                    UserName = user.UserName
                };
            }

            return View(userProfile);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfileUser(ApplicationUser model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("DangNhap");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                ModelState.AddModelError("FullName", "Họ và tên là bắt buộc.");
            }
            else if (model.FullName.Length < 2 || model.FullName.Length > 100)
            {
                ModelState.AddModelError("FullName", "Họ và tên phải từ 2 đến 100 ký tự.");
            }

            if (string.IsNullOrWhiteSpace(model.UserName))
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập là bắt buộc.");
            }
            else if (model.UserName.Length < 3 || model.UserName.Length > 50)
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập phải từ 3 đến 50 ký tự.");
            }

            // Validate phone number
            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                var phoneRegex = new Regex(@"^(0[3|5|7|8|9])+([0-9]{8})$");
                if (!phoneRegex.IsMatch(model.PhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam.");
                }
                else
                {
                    // Check if phone number already exists (excluding current user)
                    var existingPhone = await _context.Users
                        .AnyAsync(u => u.PhoneNumber == model.PhoneNumber && u.Email != user.Email);
                    if (existingPhone)
                    {
                        ModelState.AddModelError("PhoneNumber", "Số điện thoại này đã được sử dụng.");
                    }
                }
            }

            // Validate birth date
            if (model.BirthDate.HasValue)
            {
                var age = DateTime.Now.Year - model.BirthDate.Value.Year;
                if (model.BirthDate.Value.Date > DateTime.Now.AddYears(-age)) age--;

                if (age < 15)
                {
                    ModelState.AddModelError("DateOfBirth", "Bạn phải từ 13 tuổi trở lên.");
                }
                else if (age > 120)
                {
                    ModelState.AddModelError("DateOfBirth", "Ngày sinh không hợp lệ.");
                }
            }

            // Validate address length
            if (!string.IsNullOrWhiteSpace(model.Address) && model.Address.Length > 200)
            {
                ModelState.AddModelError("Address", "Địa chỉ không được vượt quá 200 ký tự.");
            }

            // Validate gender
            if (!string.IsNullOrWhiteSpace(model.Gender) &&
                !new[] { "Nam", "Nữ", "Khác" }.Contains(model.Gender))
            {
                ModelState.AddModelError("Gender", "Giới tính không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == user.Email);

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

    [HttpPost, Authorize]
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

    [HttpPost, Authorize]
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

    [HttpPost, Authorize]
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

    [Authorize]
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

        // 👇 Lấy thông tin user để tự fill UI checkout (tuỳ field của ApplicationUser nhà bạn)
        var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
        ViewBag.UserProfile = new
        {
            FullName = appUser?.FullName ?? appUser?.UserName,
            Email = appUser?.Email,
            Phone = appUser?.PhoneNumber,
            Address = appUser?.Address // nếu có
        };

        return View(cart);
    }

    // ========== 1) KHỞI TẠO THANH TOÁN → REDIRECT PAYOS ==========
    [HttpPost, Authorize]
    public async Task<IActionResult> ThanhToanQuaPayOS()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Lấy giỏ hàng
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

        // Tính tổng
        decimal total = cart.CartDetails.Sum(cd =>
            (cd.ProductVariant.SalePrice ?? 0m) * (cd.Quantity ?? 1));

        // Mã đơn tạm (duy nhất) – bạn có thể lưu trước nếu muốn idempotent chuẩn chỉnh
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Mô tả đơn
        string description = "Thanh toán đơn hàng";

        // Return/Cancel URL động
        string returnUrl = Url.Action("ThanhToanThanhCong", "User", new { orderCode }, Request.Scheme!);
        string cancelUrl = Url.Action("ThanhToanHuy", "User", new { orderCode }, Request.Scheme!);

        // Items
        var items = cart.CartDetails.Select(cd => new PayOSItem
        {
            name = cd.ProductVariant.Product?.ProductName ?? "Sản phẩm",
            quantity = cd.Quantity ?? 1,                              // int? -> int
            price = (int)(cd.ProductVariant.SalePrice ?? 0m)         // decimal? -> int
        }).ToList();


        // Gọi PayOS → nhận checkoutUrl
        var checkoutUrl = await _payOSService.CreatePaymentRequestAsync(
            total, orderCode, description, returnUrl!, cancelUrl!, items);

        if (string.IsNullOrEmpty(checkoutUrl))
        {
            TempData["PayOSError"] = _payOSService.LastError ?? "Không thể tạo yêu cầu thanh toán.";
            return RedirectToAction("GioHang");
        }

        // Sang trang thanh toán PayOS
        return Redirect(checkoutUrl);
    }

    // ========== 2) PAYOS REDIRECT: THANH TOÁN THÀNH CÔNG ==========
    // Có thể để [AllowAnonymous] nếu muốn cho phép xem kết quả dù session login hết hạn.
    [Authorize]
    public async Task<IActionResult> ThanhToanThanhCong(long orderCode)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // ĐỔI DÒNG NÀY
        var result = await _payOSService.GetPaymentStatusAsync(orderCode);

        if (result == null)
        {
            TempData["Message"] = "Không thể kiểm tra trạng thái thanh toán.";
            TempData["Error"] = _payOSService.LastError;
            return RedirectToAction("GioHang");
        }

        var status = result["data"]?["status"]?.ToString();
        if (!string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Message"] = $"Giao dịch chưa hoàn tất. Trạng thái: {status}";
            return RedirectToAction("GioHang");
        }

        var cart = _context.Carts
            .Include(c => c.CartDetails)
                .ThenInclude(cd => cd.ProductVariant)
            .FirstOrDefault(c => c.UserId == userId);

        if (cart == null || !cart.CartDetails.Any())
        {
            TempData["Message"] = "Không tìm thấy giỏ hàng để tạo đơn.";
            return RedirectToAction("GioHang");
        }

        // SỬA NULLABLE CHO CHẮC
        var totalAmount = cart.CartDetails.Sum(cd => (cd.ProductVariant.SalePrice ?? 0m) * (cd.Quantity ?? 1));
        var totalQuantity = cart.CartDetails.Sum(cd => cd.Quantity ?? 0);

        var order = new Order
        {
            UserId = userId!,
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
                Quantity = item.Quantity ?? 1,
                UnitPrice = item.ProductVariant.SalePrice ?? 0m,
                TotalPrice = (item.ProductVariant.SalePrice ?? 0m) * (item.Quantity ?? 1)
            });
        }

        _context.Orders.Add(order);
        _context.CartDetails.RemoveRange(cart.CartDetails);
        _context.SaveChanges();

        TempData["Message"] = "🎉 Thanh toán thành công và đơn hàng đã được ghi nhận!";
        return RedirectToAction("Index");
    }

    // ========== 3) PAYOS REDIRECT: NGƯỜI DÙNG HỦY hoặc FAIL ==========
    [Authorize]
    public IActionResult ThanhToanHuy(long orderCode)
    {
        TempData["Message"] = "Bạn đã huỷ giao dịch hoặc giao dịch không thành công.";
        return RedirectToAction("GioHang");
    }

    [Authorize]
    public IActionResult ThanhToan()
    {
        if (User.Identity.IsAuthenticated)
        {
            ViewBag.UserName = User.Identity.Name;
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        // Lấy thông tin user để tự fill UI checkout
        var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
        ViewBag.UserProfile = new
        {
            FullName = appUser?.FullName ?? appUser?.UserName,
            Email = appUser?.Email,
            Phone = appUser?.PhoneNumber,
            Address = appUser?.Address // nếu có
        };

        return View();
    }

    public IActionResult LienHe() => View();
    public IActionResult QuenMatKhau() => View();
    public IActionResult ChiTiet() => View();

    // ========== 4) THÊM METHOD XỬ LÝ THANH TOÁN TIỀN MẶT ==========
    [HttpPost, Authorize]
    public async Task<IActionResult> CreateCashOrder([FromBody] CheckoutPaymentRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để tiếp tục." });
            }

            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Tạo đơn hàng
                var order = new Order
                {
                    UserId = userId,
                    RecipientName = request.CustomerName,
                    RecipientPhone = request.CustomerPhone,
                    DeliveryAddress = request.FullAddress,
                    OrderDate = DateTime.Now,
                    TotalAmount = total,
                    OrderStatus = "Chờ xác nhận",
                    PaymentStatus = "Chưa thanh toán",
                    Note = request.OrderNote
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Tạo chi tiết đơn hàng
                foreach (var item in request.Items)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        ProductVariantId = item.VariantId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price,
                        TotalPrice = item.Price * item.Quantity
                    };
                    _context.OrderDetails.Add(orderDetail);
                }

                // Tạo bản ghi thanh toán
                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    MethodName = "Tiền mặt",
                    PaymentDate = DateTime.Now,
                    Amount = total,
                    PaymentStatus = "Chờ thanh toán",
                    PaymentContent = "Thanh toán khi nhận hàng",
                    IsActive = true
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Xóa sản phẩm khỏi giỏ hàng
                var cart = await _context.Carts
                    .Include(c => c.CartDetails)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart != null)
                {
                    var orderVariantIds = request.Items.Select(item => item.VariantId).ToList();
                    var cartItemsToRemove = cart.CartDetails
                        .Where(cd => orderVariantIds.Contains((int)cd.ProductVariantId))
                        .ToList();

                    _context.CartDetails.RemoveRange(cartItemsToRemove);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return Json(new { success = true, message = "Đặt hàng thành công! Đơn hàng sẽ được giao trong thời gian sớm nhất." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Có lỗi xảy ra khi tạo đơn hàng. Vui lòng thử lại." });
        }
    }

}
