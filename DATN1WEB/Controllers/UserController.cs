using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json; // for GetFromJsonAsync
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DATN1API.Data;
using DATN1API.Models;       // Product, Category, Cart, CartDetail, ProductVariant, Order, OrderDetail, Payment...
using DATN1API.Models.Pay;   // PayOSItem, CheckoutPaymentRequest
using DATN1API.Pay;          // VnPayService, PayOSService
using DATN1WEB.Models;       // ApplicationUser
using DATN1WEB.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DATNAPI1.Controllers
{
    [Authorize(Roles = "User")]
    [Authorize] // phần lớn action cần đăng nhập; những action public sẽ bỏ riêng
    public class UserController : Controller
    {
        private readonly HttpClient _client;
        private readonly DatnContext _context;
        private readonly PayOSService _payOSService;
        private readonly VnPayService _vnpayService;
        private readonly UserManager<ApplicationUser> _userManager;

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

        // ===================== CATALOG / INDEX (PUBLIC) =====================
        //[AllowAnonymous]
        //public async Task<IActionResult> Index(int? categoryId, int page = 1)
        //{
        //    if (User.Identity?.IsAuthenticated == true)
        //    {
        //        ViewBag.UserName = User.Identity!.Name;
        //    }

        //    var products = await _client.GetFromJsonAsync<List<Product>>("api/Product") ?? new();
        //    var categories = await _client.GetFromJsonAsync<List<Category>>("api/Categories") ?? new();

        //    ViewBag.Categories = categories;
        //    ViewBag.SelectedCategoryId = categoryId;

        //    if (categoryId.HasValue)
        //    {
        //        products = products.Where(p => p.CategoryId == categoryId.Value).ToList();
        //    }

        //    const int pageSize = 8;
        //    int totalProducts = products.Count;
        //    var pagedProducts = products
        //        .OrderByDescending(p => p.CreatedDate)
        //        .Skip((page - 1) * pageSize)
        //        .Take(pageSize)
        //        .ToList();

        //    ViewBag.Page = page;
        //    ViewBag.TotalPages = (int)Math.Ceiling((double)totalProducts / pageSize);
        //    ViewBag.Top4Products = products.OrderByDescending(p => p.CreatedDate).Take(4).ToList();

        //    return View(pagedProducts);
        //}
        [AllowAnonymous]
        public async Task<IActionResult> Index(int? categoryId, int page = 1, string? search = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                ViewBag.UserName = User.Identity!.Name;

            // 1) Lấy dữ liệu từ API
            var products = await _client.GetFromJsonAsync<List<Product>>("api/Product") ?? new();
            var categories = await _client.GetFromJsonAsync<List<Category>>("api/Categories") ?? new();

            // 2) Đếm số sản phẩm theo CategoryId
            var categoryCounts = products
                .Where(p => p.CategoryId.HasValue)
                .GroupBy(p => p.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            ViewBag.Categories = categories;
            ViewBag.CategoryCounts = categoryCounts;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.Search = search;

            // 3) Lọc theo category nếu có
            if (categoryId.HasValue)
                products = products.Where(p => p.CategoryId == categoryId.Value).ToList();

            // 3b) Lọc theo từ khoá search
            if (!string.IsNullOrWhiteSpace(search))
                products = products.Where(p => p.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            // 4) Sắp xếp mới nhất
            products = products
                .OrderByDescending(p => p.CreatedDate ?? DateTime.MinValue)
                .ToList();

            // 5) Phân trang 8/sp
            const int pageSize = 8;
            var totalProducts = products.Count;
            var totalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            var pagedProducts = products
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 6) Top 4 theo bộ lọc hiện tại
            ViewBag.Top4Products = products.Take(4).ToList();

            // 7) Info phân trang
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalProducts = totalProducts;

            return View(pagedProducts); // Model: List<Product>
        }

        // ===================== PROFILE =====================
        [HttpGet]
        public async Task<IActionResult> ProfileUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("DangNhap");

            var userProfile = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (userProfile == null)
            {
                // Nếu chưa có record trong DB, tạo default model cho View (không save vội)
                userProfile = new ApplicationUser
                {
                    Email = user.Email,
                    UserName = user.UserName
                };
            }

            return View(userProfile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfileUser(ApplicationUser model)
        {
            var authUser = await _userManager.GetUserAsync(User);
            if (authUser == null) return RedirectToAction("DangNhap");

            // Validate
            if (string.IsNullOrWhiteSpace(model.FullName))
                ModelState.AddModelError(nameof(model.FullName), "Họ và tên là bắt buộc.");
            else if (model.FullName.Length < 2 || model.FullName.Length > 100)
                ModelState.AddModelError(nameof(model.FullName), "Họ và tên phải từ 2 đến 100 ký tự.");

            if (string.IsNullOrWhiteSpace(model.UserName))
                ModelState.AddModelError(nameof(model.UserName), "Tên đăng nhập là bắt buộc.");
            else if (model.UserName.Length < 3 || model.UserName.Length > 50)
                ModelState.AddModelError(nameof(model.UserName), "Tên đăng nhập phải từ 3 đến 50 ký tự.");

            // Phone VN: 0 + (3|5|7|8|9) + 8 số
            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                var phoneRegex = new Regex(@"^(0[35789])[0-9]{8}$");
                if (!phoneRegex.IsMatch(model.PhoneNumber))
                {
                    ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam.");
                }
                else
                {
                    // số ĐT đã tồn tại ở user khác?
                    var existingPhone = await _context.Users
                        .AnyAsync(u => u.PhoneNumber == model.PhoneNumber && u.Email != authUser.Email);
                    if (existingPhone)
                        ModelState.AddModelError(nameof(model.PhoneNumber), "Số điện thoại này đã được sử dụng.");
                }
            }

            // BirthDate + tuổi >= 13
            if (model.BirthDate.HasValue)
            {
                var today = DateTime.Today;
                var birth = model.BirthDate.Value.Date;
                var age = today.Year - birth.Year;
                if (birth > today.AddYears(-age)) age--;

                if (age < 13)
                    ModelState.AddModelError(nameof(model.BirthDate), "Bạn phải từ 13 tuổi trở lên.");
                else if (age > 120)
                    ModelState.AddModelError(nameof(model.BirthDate), "Ngày sinh không hợp lệ.");
            }

            if (!string.IsNullOrWhiteSpace(model.Address) && model.Address.Length > 200)
                ModelState.AddModelError(nameof(model.Address), "Địa chỉ không được vượt quá 200 ký tự.");

            if (!string.IsNullOrWhiteSpace(model.Gender) && !new[] { "Nam", "Nữ", "Khác" }.Contains(model.Gender))
                ModelState.AddModelError(nameof(model.Gender), "Giới tính không hợp lệ.");

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == authUser.Email);

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
                        Email = authUser.Email,
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
                return RedirectToAction(nameof(ProfileUser));
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

        // ===================== PRODUCT VARIANT PICKER (PUBLIC) =====================
        [AllowAnonymous]
        public IActionResult ChonBienThe(int id)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                ViewBag.UserName = User.Identity!.Name;
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

        // ===================== CART =====================
        //[HttpPost]
        //public IActionResult ThemVaoGio(int variantId, int quantity)
        //{
        //    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //    if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        //    var cart = _context.Carts
        //        .Include(c => c.CartDetails)
        //        .FirstOrDefault(c => c.UserId == userId);

        //    if (cart == null)
        //    {
        //        cart = new Cart
        //        {
        //            UserId = userId,
        //            CreatedDate = DateTime.Now,
        //            LastUpdated = DateTime.Now
        //        };
        //        _context.Carts.Add(cart);
        //        _context.SaveChanges();
        //    }

        //    var existingItem = cart.CartDetails.FirstOrDefault(c => c.ProductVariantId == variantId);
        //    if (existingItem != null)
        //        existingItem.Quantity = (existingItem.Quantity ?? 0) + quantity;
        //    else
        //        _context.CartDetails.Add(new CartDetail
        //        {
        //            CartId = cart.CartId,
        //            ProductVariantId = variantId,
        //            Quantity = quantity
        //        });

        //    cart.LastUpdated = DateTime.Now;
        //    _context.SaveChanges();

        //    return RedirectToAction(nameof(Index));
        //}

        //[HttpPost]
        //public IActionResult MuaNgay(int variantId, int quantity)
        //{
        //    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //    if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

        //    var variant = _context.ProductVariants.FirstOrDefault(v => v.VariantId == variantId);
        //    if (variant == null) return NotFound("Biến thể sản phẩm không tồn tại.");

        //    var cart = _context.Carts
        //        .Include(c => c.CartDetails)
        //        .FirstOrDefault(c => c.UserId == userId);

        //    if (cart == null)
        //    {
        //        cart = new Cart
        //        {
        //            UserId = userId,
        //            CreatedDate = DateTime.Now,
        //            LastUpdated = DateTime.Now
        //        };
        //        _context.Carts.Add(cart);
        //        _context.SaveChanges();
        //    }

        //    var existingItem = cart.CartDetails.FirstOrDefault(c => c.ProductVariantId == variantId);
        //    if (existingItem != null)
        //        existingItem.Quantity = (existingItem.Quantity ?? 0) + quantity;
        //    else
        //        _context.CartDetails.Add(new CartDetail
        //        {
        //            CartId = cart.CartId,
        //            ProductVariantId = variantId,
        //            Quantity = quantity
        //        });

        //    cart.LastUpdated = DateTime.Now;
        //    _context.SaveChanges();

        //    return RedirectToAction(nameof(GioHang), new { id = cart.CartId });
        //}
        // ===================== CART =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ThemVaoGio(int variantId, int quantity)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

            quantity = Math.Max(1, quantity);

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
                existingItem.Quantity = (existingItem.Quantity ?? 0) + quantity;
            else
                _context.CartDetails.Add(new CartDetail
                {
                    CartId = cart.CartId,
                    ProductVariantId = variantId,
                    Quantity = quantity
                });

            cart.LastUpdated = DateTime.Now;
            _context.SaveChanges();

            // 👉 Về trang giỏ để thấy ngay sản phẩm vừa thêm
            return RedirectToAction(nameof(GioHang));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MuaNgay(int variantId, int quantity)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

            quantity = Math.Max(1, quantity);

            var v = _context.ProductVariants
                .Include(x => x.Product)
                .Include(x => x.Color)
                .Include(x => x.Size)
                .FirstOrDefault(x => x.VariantId == variantId);

            if (v == null) return NotFound("Biến thể không tồn tại.");

            // Đóng gói đúng format mà trang ThanhToan cần (JS sẽ đọc và đẩy vào localStorage)
            var buyNowItem = new
            {
                VariantId = v.VariantId,
                Name = v.Product?.ProductName ?? "Sản phẩm",
                Color = v.Color?.ColorName,
                Size = v.Size?.SizeName,
                Price = v.SalePrice ?? 0m,
                Quantity = quantity,
                Thumbnail = v.ThumbnailImage,
                CategoryId = v.Product?.CategoryId
            };

            TempData["BuyNow"] = JsonConvert.SerializeObject(buyNowItem);

            // Sang trang ThanhToan; trang đó sẽ nhận TempData và dựng selectedCartItems
            return RedirectToAction(nameof(ThanhToan), new { buyNow = 1 });
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
            return RedirectToAction(nameof(GioHang));
        }

        [HttpGet]
        public IActionResult GioHang()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                ViewBag.UserName = User.Identity!.Name;
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

            var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
            ViewBag.UserProfile = new
            {
                FullName = appUser?.FullName ?? appUser?.UserName,
                Email = appUser?.Email,
                Phone = appUser?.PhoneNumber,
                Address = appUser?.Address
            };

            return View(cart);
        }

        // ===================== CHECKOUT / PAYOS =====================
        [HttpPost]
        public async Task<IActionResult> ThanhToanQuaPayOS()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var cart = _context.Carts
                .Include(c => c.CartDetails)
                    .ThenInclude(cd => cd.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null || !cart.CartDetails.Any())
            {
                TempData["Message"] = "Giỏ hàng rỗng.";
                return RedirectToAction(nameof(GioHang));
            }

            decimal total = cart.CartDetails.Sum(cd =>
                (cd.ProductVariant.SalePrice ?? 0m) * (cd.Quantity ?? 1));

            long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string description = "Thanh toán đơn hàng";

            string returnUrl = Url.Action(nameof(ThanhToanThanhCong), "User", new { orderCode }, Request.Scheme!)!;
            string cancelUrl = Url.Action(nameof(ThanhToanHuy), "User", new { orderCode }, Request.Scheme!)!;

            var items = cart.CartDetails.Select(cd => new PayOSItem
            {
                name = cd.ProductVariant.Product?.ProductName ?? "Sản phẩm",
                quantity = cd.Quantity ?? 1,
                price = (int)(cd.ProductVariant.SalePrice ?? 0m)
            }).ToList();

            var checkoutUrl = await _payOSService.CreatePaymentRequestAsync(
                total, orderCode, description, returnUrl, cancelUrl, items);

            if (string.IsNullOrEmpty(checkoutUrl))
            {
                TempData["PayOSError"] = _payOSService.LastError ?? "Không thể tạo yêu cầu thanh toán.";
                return RedirectToAction(nameof(GioHang));
            }
            // Kiểm tra tồn trước khi tạo link thanh toán
            var lines = cart.CartDetails
                .GroupBy(cd => cd.ProductVariantId)
                .Select(g => new { VariantId = (int)g.Key, Qty = g.Sum(x => x.Quantity ?? 1) })
                .ToList();

            foreach (var l in lines)
            {
                var v = await _context.ProductVariants.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.VariantId == l.VariantId);
                if (v == null || (v.Stock ?? 0) < l.Qty)
                {
                    TempData["Message"] = "Một số sản phẩm không đủ hàng. Vui lòng cập nhật giỏ hàng.";
                    return RedirectToAction(nameof(GioHang));
                }
            }

            return Redirect(checkoutUrl);
        }

        [HttpGet]
        public async Task<IActionResult> ThanhToanThanhCong(long orderCode)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var result = await _payOSService.GetPaymentStatusAsync(orderCode);
            if (result == null)
            {
                TempData["Message"] = "Không thể kiểm tra trạng thái thanh toán.";
                TempData["Error"] = _payOSService.LastError;
                return RedirectToAction(nameof(GioHang));
            }

            var status = result["data"]?["status"]?.ToString();
            if (!string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Message"] = $"Giao dịch chưa hoàn tất. Trạng thái: {status}";
                return RedirectToAction(nameof(GioHang));
            }

            var cart = _context.Carts
                .Include(c => c.CartDetails)
                    .ThenInclude(cd => cd.ProductVariant)
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null || !cart.CartDetails.Any())
            {
                TempData["Message"] = "Không tìm thấy giỏ hàng để tạo đơn.";
                return RedirectToAction(nameof(GioHang));
            }

            using var tx = await _context.Database.BeginTransactionAsync();

            // ===== 1) TRỪ TỒN KHO =====
            var consume = await ConsumeStockAsync(
                cart.CartDetails.Select(cd => ((int)cd.ProductVariantId, (int)(cd.Quantity ?? 1)))
            );
            if (!consume.ok)
            {
                await tx.RollbackAsync();
                TempData["Message"] = consume.msg ?? "Xin lỗi, một số sản phẩm đã hết hàng.";
                return RedirectToAction(nameof(GioHang));
            }

            // ===== 2) TẠO ĐƠN HÀNG =====
            var totalAmount = cart.CartDetails.Sum(cd => (cd.ProductVariant.SalePrice ?? 0m) * (cd.Quantity ?? 1));
            var totalQuantity = cart.CartDetails.Sum(cd => cd.Quantity ?? 0);

            var order = new Order
            {
                UserId = userId!,
                OrderDate = DateTime.Now,
                Quantity = totalQuantity,
                TotalAmount = totalAmount,
                OrderStatus = "Chờ xác nhận",
                PaymentStatus = "Đã thanh toán",
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

            // ===== 3) XOÁ GIỎ HÀNG & LƯU =====
            _context.CartDetails.RemoveRange(cart.CartDetails);
            await _context.SaveChangesAsync();

            await tx.CommitAsync();

            TempData["Message"] = "🎉 Thanh toán thành công và đơn hàng đã được ghi nhận!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult ThanhToanHuy(long orderCode)
        {
            TempData["Message"] = "Bạn đã huỷ giao dịch hoặc giao dịch không thành công.";
            return RedirectToAction(nameof(GioHang));
        }

        [HttpGet]
        public IActionResult ThanhToan(int? buyNow = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                ViewBag.UserName = User.Identity!.Name;

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("DangNhap");

            // ---- Thông tin người dùng để prefill form (tránh dùng anonymous -> ViewBag.UserProfile) ----
            var appUser = _context.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Id == userId);

            ViewBag.UserFullName = appUser?.FullName ?? appUser?.UserName ?? "";
            ViewBag.UserEmail = appUser?.Email ?? "";
            ViewBag.UserPhone = appUser?.PhoneNumber ?? "";
            ViewBag.UserAddress = appUser?.Address ?? "";

            // ---- Khuyến mại đang hoạt động (giữ nguyên logic cũ) ----
            var now = DateTime.Now;
            var activePromotions = _context.Promotions
                .Where(p => p.StartDate <= now &&
                            (p.EndDate == null || p.EndDate >= now) &&
                            (!p.Quantity.HasValue || (p.UsedQuantity ?? 0) < p.Quantity.Value))
                .Select(p => new
                {
                    p.PromoNameCode,
                    p.PromoName,
                    p.DiscountValue,
                    p.PromoType,
                    p.Description
                })
                .ToList();
            ViewBag.ActivePromotions = activePromotions;

            // ---- Nhận item "Mua ngay" từ TempData để View ghi vào localStorage.selectedCartItems ----
            if (TempData.TryGetValue("BuyNow", out var raw) &&
                raw is string s && !string.IsNullOrWhiteSpace(s))
            {
                ViewBag.BuyNowJson = s;
                // Nếu muốn dùng ở request tiếp theo nữa thì bật Keep:
                // TempData.Keep("BuyNow");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetActivePromotions()
        {
            try
            {
                var now = DateTime.Now;
                var activePromotions = await _context.Promotions
                    .Where(p => p.StartDate <= now &&
                               (p.EndDate == null || p.EndDate >= now) &&
                               (!p.Quantity.HasValue || (p.UsedQuantity ?? 0) < p.Quantity.Value))
                    .Select(p => new
                    {
                        promoCode = p.PromoNameCode,
                        promoName = p.PromoName,
                        discountValue = p.DiscountValue,
                        promoType = p.PromoType,
                        description = p.Description,
                        startDate = p.StartDate,
                        endDate = p.EndDate,
                        remainingQuantity = p.Quantity.HasValue ?
                            p.Quantity.Value - (p.UsedQuantity ?? 0) : (int?)null
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = activePromotions,
                    message = activePromotions.Any() ? "Tải khuyến mại thành công" : "Không có khuyến mại nào đang hoạt động"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi khi tải danh sách khuyến mại: " + ex.Message
                });
            }
        }
        // ===================== CASH ORDER =====================
        [HttpPost]
        public async Task<IActionResult> CreateCashOrder([FromBody] CheckoutPaymentRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Json(new { success = false, message = "Vui lòng đăng nhập để tiếp tục." });

                var appUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (appUser == null)
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });

                if (request == null || request.Items == null || !request.Items.Any())
                    return Json(new { success = false, message = "Không có sản phẩm được chọn." });

                if (string.IsNullOrWhiteSpace(request.CustomerName) ||
                    string.IsNullOrWhiteSpace(request.CustomerPhone) ||
                    string.IsNullOrWhiteSpace(request.FullAddress))
                    return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin giao hàng." });

                // ======= TÍNH TIỀN / KHUYẾN MÃI: giữ y nguyên logic của bạn =======
                decimal itemsSubtotal = request.Items.Sum(item => item.Price * item.Quantity);
                decimal discountAmount = request.DiscountAmount.HasValue ? Math.Max(0m, request.DiscountAmount.Value) : 0m;
                if (discountAmount > itemsSubtotal) discountAmount = itemsSubtotal;
                decimal shippingFee = request.ShippingFee.HasValue ? Math.Max(0m, request.ShippingFee.Value) : 0m;

                Promotion? promotion = null;
                if (request.PromoCode.HasValue)
                {
                    int promoId = request.PromoCode.Value;
                    promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.PromoCode == promoId);

                    if (promotion != null)
                    {
                        DateTime now = DateTime.Now;
                        bool validDate = (promotion.StartDate ?? DateTime.MinValue) <= now &&
                                         (promotion.EndDate == null || promotion.EndDate >= now);
                        bool notExceeded = !promotion.Quantity.HasValue || (promotion.UsedQuantity ?? 0) < promotion.Quantity.Value;
                        bool meetMinOrder = (promotion.MinOrderAmount ?? 0m) <= itemsSubtotal;

                        if (!validDate || !notExceeded || !meetMinOrder)
                        {
                            promotion = null; // không đủ điều kiện
                        }
                        else
                        {
                            string dbType = (promotion.PromoType ?? "").Trim().ToLowerInvariant();
                            if (dbType == "phần trăm")
                            {
                                decimal pct = Math.Max(0m, promotion.DiscountValue ?? 0m);
                                decimal calc = itemsSubtotal * pct / 100m;
                                discountAmount = Math.Clamp(calc, 0m, itemsSubtotal);
                            }
                            else if (dbType == "số tiền cố định")
                            {
                                decimal val = Math.Max(0m, promotion.DiscountValue ?? 0m);
                                discountAmount = Math.Clamp(val, 0m, itemsSubtotal);
                            }
                            else if (dbType == "miễn phí vận chuyển")
                            {
                                discountAmount = 0m;
                                shippingFee = 0m;
                            }
                            else
                            {
                                promotion = null;
                            }
                        }
                    }
                }

                decimal grandTotal = itemsSubtotal - discountAmount + shippingFee;
                if (grandTotal < 0m) grandTotal = 0m;

                using var transaction = await _context.Database.BeginTransactionAsync();

                // ===== 1) TRỪ TỒN KHO TRƯỚC =====
                var consume = await ConsumeStockAsync(
                    request.Items.Select(i => (i.VariantId, i.Quantity))
                );
                if (!consume.ok)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = consume.msg ?? "Một số sản phẩm không đủ hàng." });
                }

                // ===== 2) TẠO ĐƠN HÀNG =====
                var order = new Order
                {
                    UserId = userId,
                    RecipientName = request.CustomerName,
                    RecipientPhone = request.CustomerPhone,
                    DeliveryAddress = request.FullAddress,
                    OrderDate = DateTime.Now,
                    TotalAmount = grandTotal,
                    OrderStatus = "Chờ xác nhận",
                    PaymentStatus = "Chưa thanh toán",
                    PromoCode = promotion?.PromoCode,
                    ShippingFee = shippingFee,
                    Note = request.OrderNote
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

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

                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    MethodName = "Tiền mặt",
                    PaymentDate = DateTime.Now,
                    Amount = grandTotal,
                    PaymentStatus = "Chờ thanh toán",
                    PaymentContent = "Thanh toán khi nhận hàng",
                    IsActive = true
                };
                _context.Payments.Add(payment);

                if (promotion != null)
                {
                    promotion.UsedQuantity = (promotion.UsedQuantity ?? 0) + 1;
                    _context.Promotions.Update(promotion);

                    var lastUsedPromo = new
                    {
                        PromoId = promotion.PromoCode,
                        PromoCode = promotion.PromoNameCode ?? promotion.PromoName,
                        NewUsedCount = promotion.UsedQuantity,
                        TotalCount = promotion.Quantity
                    };
                    HttpContext.Session.SetString(
                        "LastUsedPromotion",
                        System.Text.Json.JsonSerializer.Serialize(lastUsedPromo)
                    );
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Xoá các item đã đặt khỏi giỏ
                var cart = await _context.Carts
                    .Include(c => c.CartDetails)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart != null)
                {
                    var orderVariantIds = request.Items.Select(i => i.VariantId).ToList();
                    var cartItemsToRemove = cart.CartDetails
                        .Where(cd => orderVariantIds.Contains((int)cd.ProductVariantId))
                        .ToList();

                    _context.CartDetails.RemoveRange(cartItemsToRemove);
                    await _context.SaveChangesAsync();
                }

                return Json(new
                {
                    success = true,
                    message = "Đặt hàng thành công! Đơn hàng sẽ được giao trong thời gian sớm nhất."
                });
            }
            catch
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi tạo đơn hàng. Vui lòng thử lại." });
            }
        }




        // ===================== PAGES (PUBLIC) =====================
        [AllowAnonymous] public IActionResult LienHe() => View();
        [AllowAnonymous] public IActionResult QuenMatKhau() => View();
        [AllowAnonymous] public IActionResult ChiTiet() => View();

        // ===================== AUTH PAGES (placeholder) =====================
        [AllowAnonymous] public IActionResult DangNhap() => View(); // để RedirectToAction không 404
                                                                    // UserController.cs

        [AllowAnonymous]
        public IActionResult News()
        {
            // Lấy danh sách bài viết từ DB
            var newsList = _context.News
                .OrderByDescending(n => n.PostedDate)
                .ToList();

            return View(newsList); // -> Views/User/News.cshtml
        }

        // Hiển thị danh sách khuyến mãi (public)
        [AllowAnonymous]
        public IActionResult KhuyenMai()
        {
            // Lấy tất cả mã KM, sắp xếp còn hạn lên trước
            var promos = _context.Promotions
                .OrderBy(p => p.StartDate > DateTime.Now || (p.EndDate != null && p.EndDate < DateTime.Now))
                .ThenByDescending(p => p.StartDate)
                .ToList();

            return View("Promotions", promos); // Views/User/Promotions.cshtml
        }

        // theo doi don hàng


        [HttpGet]
        public async Task<IActionResult> OrderDetail(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductVariant)
                        .ThenInclude(pv => pv.Color)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductVariant)
                        .ThenInclude(pv => pv.Size)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

            if (order == null) return NotFound();

            var vm = new MyOrderDetailVM
            {
                OrderId = order.OrderId,
                OrderDate = order.OrderDate ?? DateTime.Now,
                Quantity = (int)(order.Quantity ?? order.OrderDetails.Sum(d => d.Quantity)),
                TotalAmount = order.TotalAmount ?? 0m,
                PaymentStatus = CanonPay(order.PaymentStatus),   // ✅
                OrderStatus = CanonOrder(order.OrderStatus),   // ✅
                RecipientName = order.RecipientName ?? "",
                RecipientPhone = order.RecipientPhone ?? "",
                DeliveryAddress = order.DeliveryAddress ?? "",
                Note = order.Note,
                Items = order.OrderDetails.Select(d => new MyOrderDetailItemVM
                {
                    ProductVariantId = (int)d.ProductVariantId,
                    ProductName = d.ProductVariant!.Product?.ProductName ?? "Sản phẩm",
                    ColorName = d.ProductVariant!.Color?.ColorName,
                    SizeName = d.ProductVariant!.Size?.SizeName,
                    ThumbnailImage = d.ProductVariant!.ThumbnailImage,
                    Quantity = (int)d.Quantity,
                    UnitPrice = d.UnitPrice ?? 0m,
                    TotalPrice = (decimal)(d.TotalPrice ?? (d.UnitPrice ?? 0m) * d.Quantity)
                }).ToList()
            };


            return View("OrderDetail", vm);
        }




        
        // API: huỷ đơn (chỉ khi Pending/Chờ xác nhận)


        // API: sửa thông tin giao hàng (chỉ khi Pending/Chờ xác nhận)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderInfo(UpdateOrderInfoDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["Error"] = "Vui lòng đăng nhập.";
                    return RedirectToAction(nameof(OrderDetail), new { id = dto.OrderId });
                }

                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId && o.UserId == userId);

                if (order == null)
                {
                    TempData["Error"] = "Không tìm thấy đơn hàng.";
                    return RedirectToAction(nameof(OrderDetail), new { id = dto.OrderId });
                }

                if (CanonOrder(order.OrderStatus) != "Chờ xác nhận")
                {
                    TempData["Error"] = "Không thể thay đổi thông tin vì đơn hàng đang tiến trình. Vui lòng liên hệ Shopee để được hỗ trợ.";
                    return RedirectToAction(nameof(OrderDetail), new { id = dto.OrderId });
                }

                // validate cơ bản
                if (string.IsNullOrWhiteSpace(dto.RecipientName) ||
                    string.IsNullOrWhiteSpace(dto.RecipientPhone) ||
                    string.IsNullOrWhiteSpace(dto.DeliveryAddress))
                {
                    TempData["Error"] = "Vui lòng nhập đủ Tên, SĐT, Địa chỉ.";
                    return RedirectToAction(nameof(OrderDetail), new { id = dto.OrderId });
                }

                var phoneValid = Regex.IsMatch(dto.RecipientPhone.Trim(), @"^(0[35789])[0-9]{8}$");
                if (!phoneValid)
                {
                    TempData["Error"] = "Số điện thoại không hợp lệ.";
                    return RedirectToAction(nameof(OrderDetail), new { id = dto.OrderId });
                }

                // cập nhật
                order.RecipientName = dto.RecipientName.Trim();
                order.RecipientPhone = dto.RecipientPhone.Trim();
                order.DeliveryAddress = dto.DeliveryAddress.Trim();
                order.Note = (dto.Note ?? "").Trim();

                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật thông tin giao hàng thành công.";
                return RedirectToAction(nameof(OrderDetail), new { id = dto.OrderId });
            }
            catch
            {
                TempData["Error"] = "Có lỗi xảy ra khi cập nhật. Vui lòng thử lại.";
                return RedirectToAction(nameof(OrderDetail), new { id = dto.OrderId });
            }
        }


        // ==== DTO cập nhật thông tin giao hàng ====
        public class UpdateOrderInfoDto
        {
            public int OrderId { get; set; }
            public string RecipientName { get; set; } = "";
            public string RecipientPhone { get; set; } = "";
            public string DeliveryAddress { get; set; } = "";
            public string? Note { get; set; }
        }

        // ==== Danh sách đơn của tôi (REPLACE action Orders cũ) ====
        [HttpGet]
        public async Task<IActionResult> Orders(int page = 1, string? status = null, string? q = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("DangNhap");

            const int pageSize = 10;

            static List<string> StatusAliases(string? s)
            {
                var v = (s ?? "").Trim().ToLower();
                return v switch
                {
                    "chờ xác nhận" or "pending" => new() { "Chờ xác nhận", "pending", "" },
                    "đang chuẩn bị" or "processing" or "đang xử lý" or "chờ xử lý"
                                                              => new() { "Đang chuẩn bị", "processing", "Đang xử lý", "Chờ xử lý" },
                    "đang giao" or "shipping" => new() { "Đang giao", "shipping" },
                    "hoàn tất" or "delivered" or "completed" => new() { "Hoàn tất", "delivered", "completed" },
                    "đã huỷ" or "đã hủy" or "cancelled" => new() { "Đã huỷ", "Đã hủy", "cancelled" },
                    _ => new()
                };
            }

            var query = _context.Orders.Where(o => o.UserId == userId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var aliases = StatusAliases(status);
                if (aliases.Count > 0)
                {
                    if (aliases.Contains("", StringComparer.OrdinalIgnoreCase))
                        query = query.Where(o => o.OrderStatus == null || o.OrderStatus == "" || aliases.Contains(o.OrderStatus!));
                    else
                        query = query.Where(o => aliases.Contains(o.OrderStatus!));
                }
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(o =>
                    (o.RecipientName ?? "").Contains(q) ||
                    (o.RecipientPhone ?? "").Contains(q) ||
                    (o.DeliveryAddress ?? "").Contains(q) ||
                    (o.Note ?? "").Contains(q) ||
                    o.OrderId.ToString() == q);
            }

            var total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new MyOrderListItemVM
                {
                    OrderId = o.OrderId,
                    OrderDate = o.OrderDate ?? DateTime.Now,
                    Quantity = (o.Quantity ?? o.OrderDetails.Sum(d => (int?)d.Quantity) ?? 0),
                    TotalAmount = o.TotalAmount ?? 0m,
                    PaymentStatus = CanonPay(o.PaymentStatus),   // ✅ chuẩn hoá
                    OrderStatus = CanonOrder(o.OrderStatus),   // ✅ chuẩn hoá
                    RecipientName = o.RecipientName ?? "",
                    RecipientPhone = o.RecipientPhone ?? "",
                    DeliveryAddress = o.DeliveryAddress ?? "",
                    Note = o.Note
                })
                .ToListAsync();


            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Status = status;
            ViewBag.Query = q;

            ViewBag.StatusOptions = new List<SelectListItem> {
        new("Tất cả", ""),
        new("Chờ xác nhận", "Chờ xác nhận"),
        new("Đang chuẩn bị", "Đang chuẩn bị"),
        new("Đang giao", "Đang giao"),
        new("Hoàn tất", "Hoàn tất"),
        new("Đã huỷ", "Đã huỷ"),
    };

            return View("Orders", orders);
        }

        // ==== Huỷ đơn: chỉ khi Chờ xác nhận ====
        // UserController.cs
        public class CancelOrderRequest
        {
            public int Id { get; set; }
            public string? Reason { get; set; }
        }

        // ==== Huỷ đơn: dùng TempData & Redirect ====
        // ==== Huỷ đơn: dùng TempData & Redirect ====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(CancelOrderRequest req)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["Error"] = "Vui lòng đăng nhập để tiếp tục.";
                    return RedirectToAction(nameof(Orders));
                }

                if (req == null || req.Id <= 0)
                {
                    TempData["Error"] = "Yêu cầu không hợp lệ.";
                    return RedirectToAction(nameof(Orders));
                }

                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.OrderId == req.Id && o.UserId == userId);

                if (order == null)
                {
                    TempData["Error"] = "Không tìm thấy đơn hàng.";
                    return RedirectToAction(nameof(Orders));
                }

                var st = (order.OrderStatus ?? "").Trim().ToLowerInvariant();
                var isPending = string.IsNullOrWhiteSpace(st) || st == "pending" || st == "chờ xác nhận";
                if (!isPending)
                {
                    TempData["Error"] = "Chỉ có thể huỷ khi đơn đang ở trạng thái Chờ xác nhận.";
                    return RedirectToAction(nameof(OrderDetail), new { id = order.OrderId });
                }

                if (st == "cancelled" || st == "canceled" || st == "đã huỷ" || st == "đã hủy")
                {
                    TempData["Success"] = "Đơn đã được huỷ trước đó.";
                    return RedirectToAction(nameof(OrderDetail), new { id = order.OrderId });
                }

                var reason = (req.Reason ?? "").Trim();
                if (reason.Length < 5)
                {
                    TempData["Error"] = "Lý do huỷ tối thiểu 5 ký tự.";
                    return RedirectToAction(nameof(OrderDetail), new { id = order.OrderId });
                }

                using var tx = await _context.Database.BeginTransactionAsync();

                // Hoàn kho
                var lines = order.OrderDetails.Select(d =>
                    ((int)d.ProductVariantId, (int)(d.Quantity ?? 0)));
                await RestockAsync(lines);

                // Cập nhật trạng thái
                order.OrderStatus = "Cancelled";
                if (string.Equals(order.PaymentStatus, "Đã thanh toán", StringComparison.OrdinalIgnoreCase))
                    order.PaymentStatus = "Chờ hoàn tiền";

                var prefix = $"[HUỶ BỞI KHÁCH {DateTime.Now:dd/MM/yyyy HH:mm}] ";
                order.Note = string.IsNullOrWhiteSpace(order.Note)
                    ? (prefix + reason)
                    : (prefix + reason + "\n" + order.Note);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = "Đã huỷ đơn và hoàn lại tồn kho.";
                return RedirectToAction(nameof(OrderDetail), new { id = order.OrderId });
            }
            catch
            {
                TempData["Error"] = "Có lỗi xảy ra khi huỷ đơn.";
                return RedirectToAction(nameof(OrderDetail), new { id = req?.Id });
            }
        }



        public class ReorderRequest
        {
            public int Id { get; set; } // OrderId cũ
        }

        // ==== Đặt lại đơn: dùng TempData & Redirect ====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorder(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["Error"] = "Vui lòng đăng nhập để tiếp tục.";
                    return RedirectToAction(nameof(Orders));
                }

                if (id <= 0)
                {
                    TempData["Error"] = "Yêu cầu không hợp lệ.";
                    return RedirectToAction(nameof(Orders));
                }

                var oldOrder = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

                if (oldOrder == null)
                {
                    TempData["Error"] = "Không tìm thấy đơn hàng.";
                    return RedirectToAction(nameof(Orders));
                }

                var st = (oldOrder.OrderStatus ?? "").Trim().ToLowerInvariant();
                var isCancelled = st == "cancelled" || st == "canceled" || st == "đã huỷ" || st == "đã hủy";
                if (!isCancelled)
                {
                    TempData["Error"] = "Chỉ có thể đặt lại khi đơn đã huỷ.";
                    return RedirectToAction(nameof(OrderDetail), new { id });
                }

                if (!(oldOrder.OrderDetails?.Any() ?? false))
                {
                    TempData["Error"] = "Đơn cũ không có sản phẩm để đặt lại.";
                    return RedirectToAction(nameof(OrderDetail), new { id });
                }

                var newOrder = new Order
                {
                    UserId = oldOrder.UserId,
                    OrderDate = DateTime.Now,
                    PaymentStatus = "Chưa thanh toán",
                    OrderStatus = "Chờ xác nhận",
                    RecipientName = oldOrder.RecipientName,
                    RecipientPhone = oldOrder.RecipientPhone,
                    DeliveryAddress = oldOrder.DeliveryAddress,
                    PromoCode = oldOrder.PromoCode,
                    ShippingFee = oldOrder.ShippingFee,
                    Note = $"[ĐẶT LẠI từ #{oldOrder.OrderId} - {DateTime.Now:dd/MM/yyyy HH:mm}]\n" + (oldOrder.Note ?? "")
                };

                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync();

                decimal itemsSubtotal = 0m;
                int totalQty = 0;

                foreach (var d in oldOrder.OrderDetails)
                {
                    var qty = d.Quantity ?? 0;
                    var unit = d.UnitPrice ?? 0m;
                    var line = new OrderDetail
                    {
                        OrderId = newOrder.OrderId,
                        ProductVariantId = d.ProductVariantId,
                        Quantity = qty,
                        UnitPrice = unit,
                        TotalPrice = unit * qty
                    };
                    _context.OrderDetails.Add(line);

                    itemsSubtotal += line.TotalPrice ?? 0m;
                    totalQty += qty;
                }

                newOrder.TotalAmount = itemsSubtotal;
                newOrder.Quantity = totalQty;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Đã tạo đơn mới #{newOrder.OrderId} từ đơn cũ #{oldOrder.OrderId}.";
                return RedirectToAction(nameof(OrderDetail), new { id = newOrder.OrderId });
            }
            catch
            {
                TempData["Error"] = "Có lỗi xảy ra khi đặt lại đơn.";
                return RedirectToAction(nameof(Orders));
            }
        }



        // ==== Cập nhật thông tin giao hàng: chỉ khi Chờ xác nhận ====
        //[HttpPost]
        //public async Task<IActionResult> UpdateOrderInfo([FromBody] UpdateOrderInfoDto dto)
        //{
        //    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //    var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == dto.OrderId && o.UserId == userId);
        //    if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
        //    if (CanonOrder(order.OrderStatus) != "Chờ xác nhận")

        //        return Json(new { success = false, message = "Đơn không còn ở trạng thái Chờ xác nhận." });

        //    if (string.IsNullOrWhiteSpace(dto.RecipientName) ||
        //        string.IsNullOrWhiteSpace(dto.RecipientPhone) ||
        //        string.IsNullOrWhiteSpace(dto.DeliveryAddress))
        //        return Json(new { success = false, message = "Vui lòng nhập đủ Tên, SĐT, Địa chỉ." });

        //    order.RecipientName = dto.RecipientName.Trim();
        //    order.RecipientPhone = dto.RecipientPhone.Trim();
        //    order.DeliveryAddress = dto.DeliveryAddress.Trim();
        //    order.Note = dto.Note;

        //    await _context.SaveChangesAsync();
        //    return Json(new { success = true, message = "Đã lưu thay đổi." });
        //}

        // ==== Trạng thái hiện tại (phục vụ auto-refresh) ====
        [HttpGet]
        public async Task<IActionResult> MyOrderStatus(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var o = await _context.Orders.FirstOrDefaultAsync(x => x.OrderId == id && x.UserId == userId);
            if (o == null) return Json(new { success = false });
            return Json(new { success = true, orderStatus = CanonOrder(o.OrderStatus) }); // ✅
        }

        // ===== Canon helpers =====
        private static string CanonOrder(string? s)
        {
            var x = (s ?? "").Trim().ToLowerInvariant();
            return x switch
            {
                "" or "pending" or "chờ xác nhận" => "Chờ xác nhận",
                "processing" or "đang chuẩn bị" or "đang xử lý" or "chờ xử lý" => "Đang chuẩn bị",
                "shipping" or "đang giao" => "Đang giao",
                "delivered" or "completed" or "hoàn tất" => "Hoàn tất",
                "cancelled" or "canceled" or "đã huỷ" or "đã hủy" => "Đã huỷ",
                _ => "Chờ xác nhận"
            };
        }

        private static string CanonPay(string? s)
        {
            var x = (s ?? "").Trim().ToLowerInvariant();
            return x switch
            {
                "paid" or "đã thanh toán" => "Đã thanh toán",
                "pending" or "chờ thanh toán" => "Chờ thanh toán",
                "failed" or "thất bại" => "Thanh toán thất bại",
                "" or "unpaid" or "chưa thanh toán" => "Chưa thanh toán",
                _ => "Chưa thanh toán"
            };
        }
        //icon hiện số giỏ hàng 
        [HttpGet]
        public async Task<IActionResult> CartCount()
        {
            // Lấy user hiện tại (ví dụ dùng Identity)
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            if (string.IsNullOrEmpty(userId)) return Json(new { count = 0 });

            var count = await _context.Carts
                .Where(c => c.UserId == userId)
                .SelectMany(c => c.CartDetails)
                .SumAsync(d => d.Quantity ?? 0);

            return Json(new { count });
        }
        /// <summary>
        /// Trừ tồn kho theo danh sách (VariantId, Qty) trong 1 giao dịch đang mở.
        /// Nếu có biến thể không đủ hàng sẽ trả (ok=false, msg=...).
        /// </summary>
        private async Task<(bool ok, string? msg)> ConsumeStockAsync(IEnumerable<(int VariantId, int Qty)> lines)
        {
            var grouped = lines
                .GroupBy(x => x.VariantId)
                .Select(g => new { VariantId = g.Key, Qty = g.Sum(i => i.Qty) });

            foreach (var item in grouped)
            {
                // UPDATE có điều kiện: chỉ trừ khi còn đủ
                var affected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ProductVariants
            SET Stock = Stock - {item.Qty}
            WHERE VariantId = {item.VariantId}
              AND COALESCE(Stock,0) >= {item.Qty};
        ");

                if (affected == 0)
                {
                    // Tạo message thân thiện
                    var pv = await _context.ProductVariants
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
        // UserController.cs
        private async Task RestockAsync(IEnumerable<(int VariantId, int Qty)> lines)
        {
            var grouped = lines
                .GroupBy(x => x.VariantId)
                .Select(g => new { VariantId = g.Key, Qty = g.Sum(i => i.Qty) });

            foreach (var item in grouped)
            {
                // Cộng tồn kho nguyên tử, tránh race condition
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ProductVariants
            SET Stock = COALESCE(Stock,0) + {item.Qty}
            WHERE VariantId = {item.VariantId};
        ");
            }
        }
        // Huỷ đơn bằng form (TempData + Redirect)
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> CancelOrderSubmit(int id, string? reason)
        //{
        //    try
        //    {
        //        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            TempData["Error"] = "Vui lòng đăng nhập.";
        //            return RedirectToAction(nameof(OrderDetail), new { id });
        //        }

        //        var order = await _context.Orders
        //            .Include(o => o.OrderDetails)
        //            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        //        if (order == null)
        //        {
        //            TempData["Error"] = "Không tìm thấy đơn hàng.";
        //            return RedirectToAction(nameof(OrderDetail), new { id });
        //        }

        //        var st = (order.OrderStatus ?? "").Trim().ToLowerInvariant();
        //        var isPending = string.IsNullOrWhiteSpace(st) || st == "pending" || st == "chờ xác nhận";
        //        if (!isPending)
        //        {
        //            TempData["Error"] = "Chỉ có thể huỷ khi đơn ở trạng thái Chờ xác nhận.";
        //            return RedirectToAction(nameof(OrderDetail), new { id });
        //        }

        //        reason = (reason ?? "").Trim();
        //        if (reason.Length < 5)
        //        {
        //            TempData["Error"] = "Lý do huỷ tối thiểu 5 ký tự.";
        //            return RedirectToAction(nameof(OrderDetail), new { id });
        //        }

        //        using var tx = await _context.Database.BeginTransactionAsync();

        //        // Hoàn kho
        //        var lines = order.OrderDetails.Select(d => ((int)d.ProductVariantId, (int)(d.Quantity ?? 0)));
        //        await RestockAsync(lines);

        //        // Cập nhật trạng thái
        //        order.OrderStatus = "Cancelled";
        //        if (string.Equals(order.PaymentStatus, "Đã thanh toán", StringComparison.OrdinalIgnoreCase))
        //            order.PaymentStatus = "Chờ hoàn tiền";

        //        var prefix = $"[HUỶ BỞI KHÁCH {DateTime.Now:dd/MM/yyyy HH:mm}] ";
        //        order.Note = string.IsNullOrWhiteSpace(order.Note)
        //            ? (prefix + reason)
        //            : (prefix + reason + "\n" + order.Note);

        //        await _context.SaveChangesAsync();
        //        await tx.CommitAsync();

        //        TempData["Success"] = $"Đã huỷ đơn #{id} và hoàn lại tồn kho.";
        //        return RedirectToAction(nameof(OrderDetail), new { id });
        //    }
        //    catch
        //    {
        //        TempData["Error"] = "Có lỗi xảy ra khi huỷ đơn.";
        //        return RedirectToAction(nameof(OrderDetail), new { id });
        //    }
        //}

    }
}
