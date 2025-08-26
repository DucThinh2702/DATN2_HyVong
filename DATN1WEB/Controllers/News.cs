using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DATN1API.Data;
using DATN1API.Models;

namespace DATN1WEB.Controllers
{
    public class NewsController : Controller
    {
        private readonly DatnContext _context;
        private readonly IWebHostEnvironment _env;

        public NewsController(DatnContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ============ INDEX (phân trang 5/sp) ============
        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? hasImage, int? authorId, int page = 1, int pageSize = 5)
        {
            var q = _context.News.Include(n => n.Author).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                q = q.Where(n =>
                    (n.Title ?? "").Contains(search) ||
                    (n.Content ?? "").Contains(search) ||
                    n.NewsId.ToString().Contains(search));
            }

            if (string.Equals(hasImage, "yes", StringComparison.OrdinalIgnoreCase))
                q = q.Where(n => !string.IsNullOrEmpty(n.ThumbnailImage));
            else if (string.Equals(hasImage, "no", StringComparison.OrdinalIgnoreCase))
                q = q.Where(n => string.IsNullOrEmpty(n.ThumbnailImage));

            if (authorId.HasValue)
                q = q.Where(n => n.AuthorId == authorId.Value);

            // Tổng sau khi áp bộ lọc
            var totalRecords = await q.CountAsync();
            if (pageSize <= 0) pageSize = 5;

            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            var data = await q
                .OrderByDescending(n => n.PostedDate)
                .ThenByDescending(n => n.NewsId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ViewBags cho view
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRecords = totalRecords;

            ViewBag.Search = search;
            ViewBag.HasImage = hasImage;
            ViewBag.AuthorId = authorId;

            return View(data);
        }
        // ============ CREATE ============
        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(News news, IFormFile? ThumbnailImageFile)
        {
            if (string.IsNullOrWhiteSpace(news.Title))
                ModelState.AddModelError(nameof(News.Title), "Tiêu đề là bắt buộc");

            if (!ModelState.IsValid) return View(news);

            // Upload ảnh (tuỳ chọn) -> /wwwroot/hinh
            if (ThumbnailImageFile != null && ThumbnailImageFile.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "hinh");
                Directory.CreateDirectory(uploadDir);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ThumbnailImageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ThumbnailImageFile.CopyToAsync(stream);

                news.ThumbnailImage = "/hinh/" + fileName; // giống Category
            }

            news.PostedDate = DateTime.Now;
            _context.News.Add(news);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm tin tức thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ============ EDIT ============
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null) return NotFound();
            return View(news);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, News input, IFormFile? ThumbnailImageFile)
        {
            if (id != input.NewsId) return BadRequest();

            if (string.IsNullOrWhiteSpace(input.Title))
                ModelState.AddModelError(nameof(News.Title), "Tiêu đề là bắt buộc");

            if (!ModelState.IsValid) return View(input);

            var db = await _context.News.FindAsync(id);
            if (db == null) return NotFound();

            // Cập nhật trường cơ bản
            db.Title = input.Title;
            db.Content = input.Content;
            db.AuthorId = input.AuthorId;
            // Giữ PostedDate cũ để không đổi thứ tự bài cũ (nếu muốn cập nhật, có thể set DateTime.Now)

            // Ảnh mới (nếu có)
            if (ThumbnailImageFile != null && ThumbnailImageFile.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "hinh");
                Directory.CreateDirectory(uploadDir);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ThumbnailImageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await ThumbnailImageFile.CopyToAsync(stream);

                db.ThumbnailImage = "/hinh/" + fileName; // giống Category
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật tin tức thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ============ DELETE ============
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var news = await _context.News.Include(n => n.Author).FirstOrDefaultAsync(n => n.NewsId == id);
            if (news == null) return NotFound();
            return View(news);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null) return NotFound();

            _context.News.Remove(news);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xoá tin tức thành công!";
            return RedirectToAction(nameof(Index));
        }
        // ============ DETAILS ============
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var news = await _context.News
                .Include(n => n.Author)               // nếu có bảng tác giả
                .FirstOrDefaultAsync(n => n.NewsId == id);

            if (news == null)
            {
                TempData["ErrorMessage"] = "Bài viết không tồn tại hoặc đã bị xoá.";
                return RedirectToAction(nameof(Index));
            }

            return View(news); // View: Views/News/Details.cshtml (bạn đã có)
        }

    }
}
