using DATN1API.Data;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Controllers
{
    public class ColorsController : Controller
    {
        private readonly DatnContext _context;

        public ColorsController(DatnContext context)
        {
            _context = context;
        }

        // GET: Colors
        public async Task<IActionResult> Index(string? search)
        {
            var colorQuery = _context.Colors.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                colorQuery = colorQuery.Where(c => c.ColorName.Contains(search));
            }

            var colors = await colorQuery.OrderByDescending(c => c.ColorId).ToListAsync();
            ViewBag.TotalColors = await _context.Colors.CountAsync();
            ViewBag.FilteredColorCount = colors.Count;

            var sizeQuery = _context.Sizes.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                sizeQuery = sizeQuery.Where(s => s.SizeName.Contains(search));
            }
            var sizes = await sizeQuery.OrderByDescending(s => s.SizeId).ToListAsync();
            ViewBag.TotalSizes = await _context.Sizes.CountAsync();
            ViewBag.FilteredSizeCount = sizes.Count;

            ViewBag.Sizes = sizes;
            ViewBag.Search = search;

            return View(colors);
        }

        // AJAX delete
        [HttpPost]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            var color = await _context.Colors.FindAsync(id);
            if (color == null)
                return Json(new { success = false, message = "Không tìm thấy màu" });

            bool hasProduct = await _context.ProductVariants.AnyAsync(pv => pv.ColorId == id);
            if (hasProduct)
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể xóa màu này vì vẫn còn sản phẩm đang sử dụng!"
                });
            }

            _context.Colors.Remove(color);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa màu thành công!" });
        }

        // GET: Colors/Create
        public IActionResult Create(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl; // giữ để post quay lại
            return View();
        }

        // POST: Colors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Color color, string? returnUrl)
        {
            if (!ModelState.IsValid) return View(color);

            bool exists = await _context.Colors
                .AnyAsync(c => c.ColorName.ToLower() == color.ColorName.ToLower());

            if (exists)
            {
                TempData["ErrorMessage"] = "Tên màu đã tồn tại!";
                return RedirectToAction(nameof(Create), new { returnUrl });
            }

            _context.Add(color);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm màu thành công!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        // GET: Colors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var color = await _context.Colors.FindAsync(id);
            if (color == null) return NotFound();
            return View(color);
        }

        // POST: Colors/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Color color)
        {
            if (id != color.ColorId) return NotFound();

            if (!ModelState.IsValid) return View(color);

            var existingColor = await _context.Colors.AsNoTracking()
                                    .FirstOrDefaultAsync(c => c.ColorId == id);
            if (existingColor == null) return NotFound();

            if (existingColor.ColorName.Trim().ToLower() == color.ColorName.Trim().ToLower())
            {
                TempData["ErrorMessage"] = "Vui lòng đổi thông tin trước khi lưu!";
                return RedirectToAction(nameof(Edit), new { id });
            }

            bool exists = await _context.Colors
                .AnyAsync(c => c.ColorId != id && c.ColorName.ToLower() == color.ColorName.ToLower());

            if (exists)
            {
                TempData["ErrorMessage"] = "Tên màu đã tồn tại!";
                return RedirectToAction(nameof(Edit), new { id });
            }

            _context.Update(color);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật thông tin màu thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Colors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var color = await _context.Colors.FirstOrDefaultAsync(m => m.ColorId == id);
            if (color == null) return NotFound();
            return View(color);
        }

        // POST: Colors/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var color = await _context.Colors.FindAsync(id);
            if (color == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy màu cần xóa!";
                return RedirectToAction(nameof(Index));
            }

            bool hasProduct = await _context.ProductVariants.AnyAsync(pv => pv.ColorId == id);
            if (hasProduct)
            {
                TempData["ErrorMessage"] = "Không thể xóa màu này vì vẫn còn sản phẩm đang sử dụng!";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Colors.Remove(color);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa màu thành công!";
            }
            catch
            {
                TempData["ErrorMessage"] = "Xảy ra lỗi khi xóa màu!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
