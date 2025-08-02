using DATN1API.Data;
using DATN1API.Models;
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

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Sizes.AsQueryable();

            // Nếu có tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => s.SizeName.Contains(search));
            }

            // Sắp xếp giảm dần để bản mới nhất lên đầu
            var sizes = await query
                .OrderByDescending(s => s.SizeID)
                .ToListAsync();

            // Tổng số kích cỡ
            ViewBag.TotalSizes = await _context.Sizes.CountAsync();   // tổng tất cả
            ViewBag.FilteredCount = sizes.Count;                      // tổng theo tìm kiếm

            return View(sizes);
        }


        // GET: Sizes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var size = await _context.Sizes.FirstOrDefaultAsync(m => m.SizeID == id);
            if (size == null) return NotFound();
            return View(size);
        }

            // GET: Sizes/Create
            public IActionResult Create()
            {
                return View();
            }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Size size, string? returnUrl)
        {
            if (ModelState.IsValid)
            {
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

                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl); // Quay lại trang Create Product
                else
                    return RedirectToAction(nameof(Index)); // Mặc định quay về danh sách kích cỡ
            }
            return View(size);
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
            if (id != size.SizeID) return NotFound();

            if (ModelState.IsValid)
            {
                var existingSize = await _context.Sizes.AsNoTracking()
                                       .FirstOrDefaultAsync(s => s.SizeID == id);

                if (existingSize == null) return NotFound();

                // Kiểm tra không thay đổi thông tin
                if (existingSize.SizeName.Trim().ToLower() == size.SizeName.Trim().ToLower())
                {
                    TempData["ErrorMessage"] = "Vui lòng đổi thông tin trước khi lưu!";
                    return RedirectToAction(nameof(Edit), new { id });
                }

                // Kiểm tra trùng tên kích cỡ ở bản ghi khác
                bool exists = await _context.Sizes
                    .AnyAsync(s => s.SizeID != id && s.SizeName.ToLower() == size.SizeName.ToLower());
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
            return View(size);
        }


        // GET: Sizes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var size = await _context.Sizes.FirstOrDefaultAsync(m => m.SizeID == id);
            if (size == null) return NotFound();
            return View(size);
        }

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

            // --- Kiểm tra size có sản phẩm sử dụng không ---
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
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Xảy ra lỗi khi xóa kích cỡ!";
            }

            return RedirectToAction(nameof(Index));
        }
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
