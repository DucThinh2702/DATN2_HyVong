using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DATN1API.Models;
using DATN1API.Data;

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
        public async Task<IActionResult> Index(string search, string hasImage)
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

            ViewBag.TotalCount = await _context.Categories.CountAsync();
            ViewBag.WithImageCount = await _context.Categories.CountAsync(c => !string.IsNullOrEmpty(c.CategoryImage));
            ViewBag.WithoutImageCount = await _context.Categories.CountAsync(c => string.IsNullOrEmpty(c.CategoryImage));

            var data = await q.OrderBy(c => c.CategoryId).ToListAsync();
            return View(data);
        }

        // ============ CREATE ============
        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category, IFormFile? ImageFile)
        {
            if (string.IsNullOrWhiteSpace(category.CategoryName))
                ModelState.AddModelError(nameof(Category.CategoryName), "Tên danh mục là bắt buộc");

            if (!ModelState.IsValid) return View(category);

            // Upload ảnh (tuỳ chọn)
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "hinh");
                Directory.CreateDirectory(uploadDir);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ImageFile.CopyToAsync(stream);

                category.CategoryImage = "/hinh/" + fileName;
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ============ EDIT ============
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

            if (string.IsNullOrWhiteSpace(input.CategoryName))
                ModelState.AddModelError(nameof(Category.CategoryName), "Tên danh mục là bắt buộc");

            if (!ModelState.IsValid) return View(input);

            var db = await _context.Categories.FindAsync(id);
            if (db == null) return NotFound();

            db.CategoryName = input.CategoryName;

            // Ảnh mới (nếu có)
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "hinh");
                Directory.CreateDirectory(uploadDir);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ImageFile.CopyToAsync(stream);

                db.CategoryImage = "/hinh/" + fileName;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ============ DELETE ============
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
            if (category == null) return NotFound();

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xoá danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}
