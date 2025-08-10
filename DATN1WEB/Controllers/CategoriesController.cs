using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DATN1API.Models;
using DATN1API.Data; // namespace chứa ApplicationDbContext và Category

namespace DATN1WEB.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly DatnContext _context;

        public CategoriesController(DatnContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string search, string hasImage)
        {
            var q = _context.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                q = q.Where(c => c.CategoryName.Contains(search) || c.CategoryId.ToString().Contains(search));
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

    }
}
