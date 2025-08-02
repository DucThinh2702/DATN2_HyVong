using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

                ViewBag.ShippingProviders = shippingProviders;
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

                // Kiểm tra xem ModelState có hợp lệ không
                if (!ModelState.IsValid)
                {
                    var providers = await _context.ShippingProviders
                        .Select(sp => new SelectListItem
                        {
                            Value = sp.ShippingProviderId.ToString(),
                            Text = sp.ShippingProviderName
                        }).ToListAsync();

                    ViewBag.ShippingProviders = providers;
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

            ViewBag.ShippingProviders = providers;
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

                ViewBag.ShippingProviders = providers;
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

    }
}
