using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace DATN1API.Controllers
{
    public class PromotionsController : Controller
    {
        private readonly DatnContext _context;

        public PromotionsController(DatnContext context)
        {
            _context = context;
        }

        // GET: Promotions/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var shippingProviders = await _context.ShippingProviders
                .Select(sp => new SelectListItem
                {
                    Value = sp.ShippingProviderId.ToString(),
                    Text = sp.ShippingProviderName
                }).ToListAsync();

            var categories = await _context.Categories
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryName,
                    Text = c.CategoryName
                }).ToListAsync();

            // Thêm option "Tất cả sản phẩm" vào đầu danh sách
            categories.Insert(0, new SelectListItem
            {
                Value = "Tất cả sản phẩm",
                Text = "Tất cả sản phẩm"
            });

            ViewBag.ShippingProviders = shippingProviders;
            ViewBag.Categories = categories;
            ViewBag.SelectedProviderIds = new int[0]; // Mặc định không chọn gì

            return View();
        }

        // POST: Promotions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("PromoCode,PromoName,PromoType,DiscountValue,MinOrderAmount,StartDate,EndDate,Quantity,UsedQuantity,Status,Description")]
        Promotion promotion, int[] selectedShippingProviderIds)
        {
            // Kiểm tra nếu không chọn ít nhất một đơn vị vận chuyển
            if (selectedShippingProviderIds == null || selectedShippingProviderIds.Length == 0)
            {
                ModelState.AddModelError("selectedShippingProviderIds", "Vui lòng chọn ít nhất một đơn vị vận chuyển.");
            }

            // Lấy các đơn vị vận chuyển đã chọn
            var selectedProviders = await _context.ShippingProviders
                .Where(sp => selectedShippingProviderIds.Contains(sp.ShippingProviderId))
                .ToListAsync();

            // Gán các đơn vị vận chuyển đã chọn vào Promotion
            promotion.ShippingProviders = selectedProviders;
            // Lưu tên các đơn vị vận chuyển vào trường ShippingProviderName
            promotion.ShippingProviderName = string.Join(", ", selectedProviders.Select(sp => sp.ShippingProviderName));

            // Tạo mã giảm giá ngẫu nhiên
            promotion.PromoNameCode = Promotion.GeneratePromoNameCode();

            // Validate lại model sau khi gán ShippingProviders
            TryValidateModel(promotion);

            // Kiểm tra các điều kiện khác
            if (promotion.StartDate >= promotion.EndDate)
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn ngày bắt đầu.");

            if (promotion.PromoType == "Phần trăm" && (promotion.DiscountValue < 0 || promotion.DiscountValue > 100))
                ModelState.AddModelError("DiscountValue", "Phần trăm phải từ 0 đến 100.");

            if (promotion.PromoType == "Số tiền cố định" && promotion.DiscountValue > promotion.MinOrderAmount)
                ModelState.AddModelError("DiscountValue", "Giảm giá không vượt quá giá trị đơn hàng tối thiểu.");

            if (await CheckPromoNameExists(promotion.PromoName))
                ModelState.AddModelError("PromoName", "Tên mã giảm giá đã tồn tại.");
            else if (promotion.PromoName.Length < 10)
            {
                ModelState.AddModelError("PromoName", "Tên mã giảm giá phải có ít nhất 3 ký tự.");
            }
            else if (promotion.PromoName.Length > 50)
            {
                ModelState.AddModelError("PromoName", "Tên mã giảm giá không được vượt quá 100 ký tự.");
            }

            // Kiểm tra xem ModelState có hợp lệ không
            if (!ModelState.IsValid)
            {
                var providers = await _context.ShippingProviders
                    .Select(sp => new SelectListItem
                    {
                        Value = sp.ShippingProviderId.ToString(),
                        Text = sp.ShippingProviderName
                    }).ToListAsync();

                var categories = await _context.Categories
                    .Select(c => new SelectListItem
                    {
                        Value = c.CategoryName,
                        Text = c.CategoryName
                    }).ToListAsync();

                categories.Insert(0, new SelectListItem
                {
                    Value = "Tất cả sản phẩm",
                    Text = "Tất cả sản phẩm"
                });

                ViewBag.ShippingProviders = providers;
                ViewBag.Categories = categories;
                ViewBag.SelectedProviderIds = selectedShippingProviderIds;
                return View(promotion);
            }

            _context.Add(promotion);
            await _context.SaveChangesAsync();
            return RedirectToAction("MaGiamGia", "Admin");
        }

        // Kiểm tra nếu tên mã giảm giá đã tồn tại trong cơ sở dữ liệu
        private async Task<bool> CheckPromoNameExists(string promoName)
        {
            if (string.IsNullOrWhiteSpace(promoName))
                return false;

            string normalized = promoName.Trim().ToLower();

            return await _context.Promotions
                .AsNoTracking()
                .AnyAsync(p => p.PromoName.ToLower() == normalized);
        }

        // GET: Promotions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var promotion = await _context.Promotions
                .Include(p => p.ShippingProviders) // ⚠️ Bắt buộc phải có dòng này
                .FirstOrDefaultAsync(m => m.PromoCode == id);

            if (promotion == null)
                return NotFound();

            return View(promotion);
        }

        // GET: Promotions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var promotion = await _context.Promotions
                .Include(p => p.ShippingProviders)  // Đảm bảo bao gồm ShippingProviders
                .FirstOrDefaultAsync(p => p.PromoCode == id);

            if (promotion == null) return NotFound();

            var selectedProviderIds = promotion.ShippingProviders.Select(sp => sp.ShippingProviderId).ToArray();
            var providers = await _context.ShippingProviders
                .Select(sp => new SelectListItem
                {
                    Value = sp.ShippingProviderId.ToString(),
                    Text = sp.ShippingProviderName,
                    Selected = selectedProviderIds.Contains(sp.ShippingProviderId)
                }).ToListAsync();

            var categories = await _context.Categories
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryName,
                    Text = c.CategoryName,
                    Selected = c.CategoryName == promotion.Status
                }).ToListAsync();

            categories.Insert(0, new SelectListItem
            {
                Value = "Tất cả sản phẩm",
                Text = "Tất cả sản phẩm",
                Selected = promotion.Status == "Tất cả sản phẩm"
            });

            ViewBag.ShippingProviders = providers;
            ViewBag.Categories = categories;
            ViewBag.SelectedProviderIds = selectedProviderIds;

            return View(promotion);
        }

        // POST: Promotions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("PromoCode,PromoName,PromoType,DiscountValue,MinOrderAmount,StartDate,EndDate,Quantity,UsedQuantity,Status,Description")]
Promotion promotion, int[] selectedShippingProviderIds)
        {
            if (id != promotion.PromoCode)
                return NotFound();

            var existingPromotion = await _context.Promotions
                .Include(p => p.ShippingProviders)
                .FirstOrDefaultAsync(p => p.PromoCode == id);

            if (existingPromotion == null)
                return NotFound();

            // Lấy các đơn vị vận chuyển đã chọn
            var selectedProviders = await _context.ShippingProviders
                .Where(sp => selectedShippingProviderIds.Contains(sp.ShippingProviderId))
                .ToListAsync();

            existingPromotion.PromoName = promotion.PromoName;
            existingPromotion.PromoType = promotion.PromoType;
            existingPromotion.DiscountValue = promotion.DiscountValue;
            existingPromotion.MinOrderAmount = promotion.MinOrderAmount;
            existingPromotion.StartDate = promotion.StartDate;
            existingPromotion.EndDate = promotion.EndDate;
            existingPromotion.Quantity = promotion.Quantity;
            existingPromotion.UsedQuantity = promotion.UsedQuantity;
            existingPromotion.Status = promotion.Status;
            existingPromotion.Description = promotion.Description;
            existingPromotion.ShippingProviderName = string.Join(", ", selectedProviders.Select(sp => sp.ShippingProviderName));

            existingPromotion.ShippingProviders.Clear();
            foreach (var sp in selectedProviders)
                existingPromotion.ShippingProviders.Add(sp);

            // Validate lại
            TryValidateModel(existingPromotion);

            // Kiểm tra các điều kiện
            if (selectedShippingProviderIds == null || selectedShippingProviderIds.Length == 0)
                ModelState.AddModelError("selectedShippingProviderIds", "Vui lòng chọn ít nhất một đơn vị vận chuyển.");

            if (promotion.StartDate >= promotion.EndDate)
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn ngày bắt đầu.");

            if (promotion.PromoType == "Phần trăm" && (promotion.DiscountValue < 0 || promotion.DiscountValue > 100))
                ModelState.AddModelError("DiscountValue", "Phần trăm phải từ 0 đến 100.");

            if (promotion.PromoType == "Số tiền cố định" && promotion.DiscountValue > promotion.MinOrderAmount)
                ModelState.AddModelError("DiscountValue", "Giảm giá không vượt quá giá trị đơn hàng tối thiểu.");

            if (await CheckPromoNameExists(promotion.PromoName, id))
                ModelState.AddModelError("PromoName", "Tên mã giảm giá đã tồn tại.");
            else if (promotion.PromoName.Length < 10)
            {
                ModelState.AddModelError("PromoName", "Tên mã giảm giá phải có ít nhất 3 ký tự.");
            }
            else if (promotion.PromoName.Length > 50)
            {
                ModelState.AddModelError("PromoName", "Tên mã giảm giá không được vượt quá 100 ký tự.");
            }
            // Kiểm tra lại ModelState
            if (!ModelState.IsValid)
            {
                var providers = await _context.ShippingProviders
                    .Select(sp => new SelectListItem
                    {
                        Value = sp.ShippingProviderId.ToString(),
                        Text = sp.ShippingProviderName,
                        Selected = selectedShippingProviderIds.Contains(sp.ShippingProviderId)
                    }).ToListAsync();

                var categories = await _context.Categories
                    .Select(c => new SelectListItem
                    {
                        Value = c.CategoryName,
                        Text = c.CategoryName,
                        Selected = c.CategoryName == promotion.Status
                    }).ToListAsync();

                categories.Insert(0, new SelectListItem
                {
                    Value = "Tất cả sản phẩm",
                    Text = "Tất cả sản phẩm",
                    Selected = promotion.Status == "Tất cả sản phẩm"
                });

                ViewBag.ShippingProviders = providers;
                ViewBag.Categories = categories;
                ViewBag.SelectedProviderIds = selectedShippingProviderIds;
                return View(promotion);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("MaGiamGia", "Admin");
        }

        // Kiểm tra nếu tên mã giảm giá đã tồn tại trong cơ sở dữ liệu (sửa)
        private async Task<bool> CheckPromoNameExists(string promoName, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(promoName))
                return false;

            // So sánh với collation không phân biệt hoa thường
            var query = _context.Promotions
                .AsNoTracking()
                .Where(p => EF.Functions.Collate(p.PromoName, "SQL_Latin1_General_CP1_CI_AS") == promoName);

            if (excludeId.HasValue)
                query = query.Where(p => p.PromoCode != excludeId.Value);

            return await query.AnyAsync();
        }

        private bool PromotionExists(int id)
        {
            return _context.Promotions.Any(e => e.PromoCode == id);
        }

        // GET: Promotions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var promotion = await _context.Promotions
                .Include(p => p.ShippingProviders) // 👈 phải include để có danh sách
                .FirstOrDefaultAsync(m => m.PromoCode == id);

            if (promotion == null)
                return NotFound();

            return View(promotion);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotion = await _context.Promotions
                .Include(p => p.ShippingProviders)
                .FirstOrDefaultAsync(p => p.PromoCode == id);

            if (promotion == null)
                return NotFound();

            // 👉 Bước 1: Set FK trong ShippingProviders về null
            foreach (var provider in promotion.ShippingProviders)
            {
                provider.PromoCode = null;
                _context.Update(provider);
            }

            // 👉 Bước 2: Xóa Promotion
            _context.Promotions.Remove(promotion);

            await _context.SaveChangesAsync();

            return RedirectToAction("MaGiamGia", "Admin");
        }

        [HttpGet]
        [Route("api/promotions/active")]
        public async Task<IActionResult> GetActivePromotions()
        {
            try
            {
                var currentDate = DateTime.Now;

                var activePromotions = await _context.Promotions
                    .Include(p => p.ShippingProviders)
                    .Where(p =>
                        p.StartDate <= currentDate &&
                        p.EndDate >= currentDate &&
                        (p.UsedQuantity ?? 0) < (p.Quantity ?? 0) &&
                        !string.IsNullOrEmpty(p.Status))
                    .Select(p => new
                    {
                        promotionId = p.PromoCode,
                        promotionCode = p.PromoNameCode ?? p.PromoName,
                        description = p.Description,
                        discountType = p.PromoType == "Phần trăm" ? "percentage" :
                                       p.PromoType == "Miễn phí vận chuyển" ? "free_shipping" : "amount",
                        discountValue = p.DiscountValue ?? 0,
                        maxDiscountAmount = (decimal?)null,
                        minOrderValue = p.MinOrderAmount ?? 0,
                        startDate = p.StartDate,
                        endDate = p.EndDate,
                        usageLimit = p.Quantity,
                        usedCount = p.UsedQuantity ?? 0,

                        // ✅ Lấy CategoryId thật từ DB (scalar subquery)
                        categoryId = _context.Categories
                            .Where(c => c.CategoryName == p.Status)
                            .Select(c => (int?)c.CategoryId)
                            .FirstOrDefault(),

                        shippingProviders = p.ShippingProviders.Select(sp => new
                        {
                            id = sp.ShippingProviderId,
                            name = sp.ShippingProviderName
                        }).ToList()
                    })
                    .OrderBy(p => p.endDate)
                    .ToListAsync();

                // Chuẩn hoá trường status ở payload trả về: 0 = tất cả, hoặc CategoryId thật
                var shaped = activePromotions.Select(x => new
                {
                    x.promotionId,
                    x.promotionCode,
                    x.description,
                    x.discountType,
                    x.discountValue,
                    x.maxDiscountAmount,
                    x.minOrderValue,
                    x.startDate,
                    x.endDate,
                    x.usageLimit,
                    x.usedCount,
                    status = x.categoryId.GetValueOrDefault(0), // 0 = Tất cả sản phẩm hoặc không map được
                    x.shippingProviders
                });

                return Json(new { success = true, data = shaped });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể tải danh sách khuyến mại: " + ex.Message });
            }
        }


        [HttpPost]
        [Route("api/promotions/validate/{code}")]
        public async Task<IActionResult> ValidatePromotionCode(string code, [FromBody] PromotionValidationRequest request)
        {
            try
            {
                var currentDate = DateTime.Now;

                var promotion = await _context.Promotions
                    .Include(p => p.ShippingProviders)
                    .FirstOrDefaultAsync(p =>
                        (p.PromoNameCode == code || p.PromoName == code) &&
                        p.StartDate <= currentDate &&
                        p.EndDate >= currentDate &&
                        (p.UsedQuantity ?? 0) < (p.Quantity ?? 0));

                if (promotion == null)
                    return Json(new { success = false, message = "Mã khuyến mại không tồn tại hoặc đã hết hạn" });

                // Tổng đơn để check min
                decimal cartTotal = request?.CartItems?.Sum(i => i.Price * i.Quantity) ?? 0m;
                if (cartTotal < (promotion.MinOrderAmount ?? 0))
                    return Json(new { success = false, message = $"Đơn hàng tối thiểu {(promotion.MinOrderAmount ?? 0):N0} VNĐ để sử dụng mã này" });

                // ✅ So khớp theo CategoryId thực
                if (!string.Equals(promotion.Status, "Tất cả sản phẩm", StringComparison.OrdinalIgnoreCase))
                {
                    var requiredCategoryId = await _context.Categories
                        .Where(c => c.CategoryName == promotion.Status)
                        .Select(c => (int?)c.CategoryId)
                        .FirstOrDefaultAsync();

                    if (requiredCategoryId.HasValue && (request?.CartItems != null))
                    {
                        var ok = request.CartItems.Any(i => i.CategoryId == requiredCategoryId.Value);
                        if (!ok)
                            return Json(new { success = false, message = $"Mã khuyến mại chỉ áp dụng cho danh mục: {promotion.Status}" });
                    }
                }

                // Payload trả về cho front
                var result = new
                {
                    promotionId = promotion.PromoCode,
                    promotionCode = promotion.PromoNameCode ?? promotion.PromoName,
                    description = promotion.Description,
                    discountType = promotion.PromoType == "Phần trăm" ? "percentage" :
                                   promotion.PromoType == "Miễn phí vận chuyển" ? "free_shipping" : "amount",
                    discountValue = promotion.DiscountValue ?? 0,
                    maxDiscountAmount = (decimal?)null,
                    minOrderValue = promotion.MinOrderAmount ?? 0,
                    startDate = promotion.StartDate,
                    endDate = promotion.EndDate,
                    status = await _context.Categories
                                .Where(c => c.CategoryName == promotion.Status)
                                .Select(c => (int?)c.CategoryId)
                                .FirstOrDefaultAsync() ?? 0
                };

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra khi kiểm tra mã khuyến mại: " + ex.Message });
            }
        }

        [HttpPost]
        [Route("api/promotions/use/{code}")]
        public async Task<IActionResult> UsePromotionCode(string code)
        {
            try
            {
                var promotion = await _context.Promotions
                    .FirstOrDefaultAsync(p => p.PromoNameCode == code || p.PromoName == code);

                if (promotion == null)
                    return Json(new { success = false, message = "Mã khuyến mại không tồn tại" });

                promotion.UsedQuantity = (promotion.UsedQuantity ?? 0) + 1;

                _context.Update(promotion);
                await _context.SaveChangesAsync();

                // Store the last used promotion in session
                var lastUsedPromo = new
                {
                    PromoId = promotion.PromoCode,
                    PromoCode = promotion.PromoNameCode ?? promotion.PromoName,
                    NewUsedCount = promotion.UsedQuantity,
                    TotalCount = promotion.Quantity
                };
                HttpContext.Session.SetString("LastUsedPromotion", System.Text.Json.JsonSerializer.Serialize(lastUsedPromo));

                return Json(new
                {
                    success = true,
                    message = "Đã cập nhật số lượng sử dụng mã giảm giá",
                    usedQuantity = promotion.UsedQuantity,
                    remainingQuantity = (promotion.Quantity ?? 0) - (promotion.UsedQuantity ?? 0)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpGet]
        [Route("api/promotions/stats")]
        public async Task<IActionResult> GetPromotionStats()
        {
            try
            {
                var currentDate = DateTime.Now;

                var stats = new
                {
                    totalPromotions = await _context.Promotions.CountAsync(),
                    activePromotions = await _context.Promotions
                        .CountAsync(p => p.StartDate <= currentDate && p.EndDate >= currentDate),
                    totalUsed = await _context.Promotions.SumAsync(p => p.UsedQuantity ?? 0),
                    totalSavings = await _context.Promotions
                        .Where(p => p.UsedQuantity > 0)
                        .SumAsync(p => (p.UsedQuantity ?? 0) * (p.DiscountValue ?? 0))
                };

                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [Route("api/promotions/recent-usage")]
        public IActionResult GetRecentPromotionUsage()
        {
            try
            {
                var lastUsedPromoJson = HttpContext.Session.GetString("LastUsedPromotion");
                if (!string.IsNullOrEmpty(lastUsedPromoJson))
                {
                    HttpContext.Session.Remove("LastUsedPromotion");
                    var lastUsedPromo = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(lastUsedPromoJson);

                    return Json(new
                    {
                        success = true,
                        hasUpdate = true,
                        data = new
                        {
                            promoId = lastUsedPromo.GetProperty("PromoId").GetInt32(),
                            promoCode = lastUsedPromo.GetProperty("PromoCode").GetString(),
                            newUsedCount = lastUsedPromo.GetProperty("NewUsedCount").GetInt32(),
                            totalCount = lastUsedPromo.GetProperty("TotalCount").GetInt32()
                        }
                    });
                }

                return Json(new { success = true, hasUpdate = false });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

    }

    public class PromotionValidationRequest
    {
        public List<CartItemForValidation> CartItems { get; set; } = new List<CartItemForValidation>();
    }

    public class CartItemForValidation
    {
        public int? CategoryId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
