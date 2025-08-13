using DATN1API.Data;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Controllers
{
    public class SizesController : Controller
    {
        private readonly DatnContext _context;

        public SizesController(DatnContext context)
        {
            _context = context;
        }

        // GET: Sizes
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Sizes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => s.SizeName.Contains(search));
            }

            var sizes = await query
                .OrderByDescending(s => s.SizeId)
                .ToListAsync();

            ViewBag.TotalSizes = await _context.Sizes.CountAsync();
            ViewBag.FilteredCount = sizes.Count;

            return View(sizes);
        }

        // GET: Sizes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var size = await _context.Sizes.FirstOrDefaultAsync(m => m.SizeId == id);
            if (size == null) return NotFound();
            return View(size);
        }

        // GET: Sizes/Create
        public IActionResult Create(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl; // giữ để post quay lại
            return View();
        }

        // POST: Sizes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Size size, string? returnUrl)
        {
            if (!ModelState.IsValid) return View(size);

            bool exists = await _context.Sizes
                .AnyAsync(s => s.SizeName.ToLower() == size.SizeName.ToLower());

            if (exists)
            {
                TempData["ErrorMessage"] = "Tên kích cỡ đã tồn tại!";
                return RedirectToAction(nameof(Create), new { returnUrl });
            }

            _context.Add(size);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm kích cỡ thành công!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        // GET: Sizes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var size = await _context.Sizes.FindAsync(id);
            if (size == null) return NotFound();
            return View(size);
        }

        // POST: Sizes/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Size size)
        {
            if (id != size.SizeId) return NotFound();

            if (!ModelState.IsValid) return View(size);

            var existingSize = await _context.Sizes.AsNoTracking()
                                   .FirstOrDefaultAsync(s => s.SizeId == id);
            if (existingSize == null) return NotFound();

            if (existingSize.SizeName.Trim().ToLower() == size.SizeName.Trim().ToLower())
            {
                TempData["ErrorMessage"] = "Vui lòng đổi thông tin trước khi lưu!";
                return RedirectToAction(nameof(Edit), new { id });
            }

            bool exists = await _context.Sizes
                .AnyAsync(s => s.SizeId != id && s.SizeName.ToLower() == size.SizeName.ToLower());
            if (exists)
            {
                TempData["ErrorMessage"] = "Tên kích cỡ đã tồn tại!";
                return RedirectToAction(nameof(Edit), new { id });
            }

            _context.Update(size);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật kích cỡ thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Sizes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var size = await _context.Sizes.FirstOrDefaultAsync(m => m.SizeId == id);
            if (size == null) return NotFound();
            return View(size);
        }

        // POST: Sizes/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var entity = await _context.Sizes.FindAsync(id);
            if (entity == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy kích cỡ cần xóa!";
                return RedirectToAction(nameof(Index));
            }

            bool hasProduct = await _context.ProductVariants.AnyAsync(pv => pv.SizeId == id);
            if (hasProduct)
            {
                TempData["ErrorMessage"] = "Không thể xóa kích cỡ này vì vẫn còn sản phẩm đang sử dụng!";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Sizes.Remove(entity);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa kích cỡ thành công!";
            }
            catch
            {
                TempData["ErrorMessage"] = "Xảy ra lỗi khi xóa kích cỡ!";
            }

            return RedirectToAction(nameof(Index));
        }

        // AJAX delete
        [HttpPost]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            var size = await _context.Sizes.FindAsync(id);
            if (size == null)
                return Json(new { success = false, message = "Không tìm thấy kích cỡ" });

            bool hasProduct = await _context.ProductVariants.AnyAsync(pv => pv.SizeId == id);
            if (hasProduct)
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể xóa kích cỡ này vì vẫn còn sản phẩm đang sử dụng!"
                });
            }

            _context.Sizes.Remove(size);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa kích cỡ thành công!" });
        }
    }
}
