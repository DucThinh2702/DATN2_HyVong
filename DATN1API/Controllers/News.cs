using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsController : ControllerBase
    {
        private readonly DatnContext _context;

        public NewsController(DatnContext context)
        {
            _context = context;
        }

        // GET: api/News
        [HttpGet]
        public async Task<ActionResult<IEnumerable<News>>> GetNews()
        {
            return await _context.News
                .Include(n => n.Author)
                .OrderByDescending(n => n.PostedDate)
                .ToListAsync();
        }

        // GET: api/News/5
        [HttpGet("{id}")]
        public async Task<ActionResult<News>> GetNews(int id)
        {
            var news = await _context.News
                .Include(n => n.Author)
                .FirstOrDefaultAsync(n => n.NewsId == id);

            if (news == null)
                return NotFound();

            return news;
        }

        // POST: api/News
        [HttpPost]
        public async Task<ActionResult<News>> PostNews(News news)
        {
            news.PostedDate = DateTime.Now;
            _context.News.Add(news);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetNews), new { id = news.NewsId }, news);
        }

        // PUT: api/News/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutNews(int id, News news)
        {
            if (id != news.NewsId)
                return BadRequest();

            _context.Entry(news).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.News.Any(e => e.NewsId == id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/News/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNews(int id)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null)
                return NotFound();

            _context.News.Remove(news);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
