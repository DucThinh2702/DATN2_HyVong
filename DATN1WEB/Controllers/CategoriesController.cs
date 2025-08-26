using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DATN1API.Models;
using DATN1API.Data;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace DATN1WEB.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly DatnContext _context;
        private readonly IWebHostEnvironment _env;

        public CategoriesController(DatnContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ============ INDEX (giữ nguyên code cũ) ============
        // ============ INDEX (có phân trang 5/sp) ============
        public async Task<IActionResult> Index(string? search, string? hasImage, int page = 1, int pageSize = 5)
        {
            var q = _context.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                q = q.Where(c => c.CategoryName!.Contains(search) || c.CategoryId.ToString().Contains(search));
            }

            if (string.Equals(hasImage, "yes", StringComparison.OrdinalIgnoreCase))
                q = q.Where(c => !string.IsNullOrEmpty(c.CategoryImage));
            else if (string.Equals(hasImage, "no", StringComparison.OrdinalIgnoreCase))
                q = q.Where(c => string.IsNullOrEmpty(c.CategoryImage));

            // Thống kê tổng thể (không theo filter)
            ViewBag.TotalCount = await _context.Categories.CountAsync();
            ViewBag.WithImageCount = await _context.Categories.CountAsync(c => !string.IsNullOrEmpty(c.CategoryImage));
            ViewBag.WithoutImageCount = await _context.Categories.CountAsync(c => string.IsNullOrEmpty(c.CategoryImage));

            // Tổng bản ghi sau filter
            var totalRecords = await q.CountAsync();

            // Tính trang
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            // Lấy dữ liệu trang hiện tại
            var data = await q.AsNoTracking()
                              .OrderBy(c => c.CategoryId)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync();

            // Truyền ra View
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;      // = 5
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = totalRecords;

            ViewBag.Search = search ?? "";
            ViewBag.HasImage = hasImage ?? "";

            return View(data);
        }
        

        /// <summary>Chuẩn hoá tên: Trim và gom khoảng trắng</summary>
        private static string NormalizeName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim();
            // gom nhiều khoảng trắng về 1
            t = Regex.Replace(t, @"\s+", " ");
            return t;
        }
        // ========= CREATE =========
        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category, IFormFile? ImageFile)
        {
            // bắt buộc tên
            category.CategoryName = NormalizeName(category.CategoryName);
            if (string.IsNullOrWhiteSpace(category.CategoryName))
                ModelState.AddModelError(nameof(Category.CategoryName), "Tên danh mục là bắt buộc.");

            // kiểm tra trùng tên (không phân biệt hoa/thường)
            if (await _context.Categories
                .AnyAsync(c => c.CategoryName != null &&
                               c.CategoryName.Trim().ToLower() == category.CategoryName.ToLower()))
            {
                ModelState.AddModelError(nameof(Category.CategoryName), "Tên danh mục đã tồn tại.");
            }

            if (!ModelState.IsValid) return View(category);

            // ảnh (tuỳ chọn)
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "hinh");
                Directory.CreateDirectory(uploadDir);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await ImageFile.CopyToAsync(stream);
                category.CategoryImage = "/hinh/" + fileName;
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ========= EDIT =========
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category input, IFormFile? ImageFile)
        {
            if (id != input.CategoryId) return BadRequest();

            input.CategoryName = NormalizeName(input.CategoryName);
            if (string.IsNullOrWhiteSpace(input.CategoryName))
                ModelState.AddModelError(nameof(Category.CategoryName), "Tên danh mục là bắt buộc.");

            // trùng tên với 1 danh mục khác
            if (await _context.Categories
                .AnyAsync(c => c.CategoryId != id &&
                               c.CategoryName != null &&
                               c.CategoryName.Trim().ToLower() == input.CategoryName.ToLower()))
            {
                ModelState.AddModelError(nameof(Category.CategoryName), "Tên danh mục đã tồn tại.");
            }

            if (!ModelState.IsValid) return View(input);

            var db = await _context.Categories.FindAsync(id);
            if (db == null) return NotFound();

            db.CategoryName = input.CategoryName;

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "hinh");
                Directory.CreateDirectory(uploadDir);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await ImageFile.CopyToAsync(stream);
                db.CategoryImage = "/hinh/" + fileName;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                TempData["ErrorMessage"] = "Danh mục không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            // 1) Chặn từ sớm nếu còn sản phẩm
            bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
            {
                TempData["ErrorMessage"] =
                    "Danh mục vẫn còn sản phẩm, không thể xóa.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xoá danh mục thành công!";
            }
            catch (DbUpdateException ex)
            {
                // 2) Phòng trường hợp lỗi DB khác/đề phòng concurrency
                var sqlEx = ex.GetBaseException() as SqlException;
                if (sqlEx?.Number == 547) // lỗi vi phạm FK
                {
                    TempData["ErrorMessage"] =
                        "Danh mục vẫn còn dữ liệu tham chiếu (sản phẩm). Không thể xóa.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể xoá danh mục do lỗi cơ sở dữ liệu.";
                }
            }
            catch
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xoá danh mục.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
