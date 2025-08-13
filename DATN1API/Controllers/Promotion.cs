using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromotionsApiController : ControllerBase
    {
        private readonly DatnContext _context;

        public PromotionsApiController(DatnContext context)
        {
            _context = context;
        }

        // GET: api/Promotions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Promotion>>> GetPromotions()
        {
            var promotions = await _context.Promotions
                .Include(p => p.ShippingProviders) // Bao gồm các đơn vị vận chuyển
                .ToListAsync();
            return Ok(promotions);
        }

        // GET: api/Promotions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Promotion>> GetPromotion(int id)
        {
            var promotion = await _context.Promotions
                .Include(p => p.ShippingProviders)
                .FirstOrDefaultAsync(p => p.PromoCode == id);

            if (promotion == null)
            {
                return NotFound();
            }

            return Ok(promotion);
        }

        // POST: api/Promotions
        [HttpPost]
        public async Task<ActionResult<Promotion>> PostPromotion([FromBody] Promotion promotion)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra mã giảm giá đã tồn tại
            if (await CheckPromoNameExists(promotion.PromoName))
            {
                return BadRequest("Tên mã giảm giá đã tồn tại.");
            }

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPromotion), new { id = promotion.PromoCode }, promotion);
        }

        // PUT: api/Promotions/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPromotion(int id, [FromBody] Promotion promotion)
        {
            if (id != promotion.PromoCode)
            {
                return BadRequest();
            }

            var existingPromotion = await _context.Promotions
                .Include(p => p.ShippingProviders)
                .FirstOrDefaultAsync(p => p.PromoCode == id);

            if (existingPromotion == null)
            {
                return NotFound();
            }

            // Cập nhật các thuộc tính của promotion
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

            // Cập nhật danh sách ShippingProviders
            existingPromotion.ShippingProviderName = string.Join(", ", promotion.ShippingProviders.Select(sp => sp.ShippingProviderName));
            existingPromotion.ShippingProviders = promotion.ShippingProviders;

            _context.Entry(existingPromotion).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Promotions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            var promotion = await _context.Promotions
                .Include(p => p.ShippingProviders)
                .FirstOrDefaultAsync(p => p.PromoCode == id);

            if (promotion == null)
            {
                return NotFound();
            }

            // Cập nhật FK trong ShippingProviders về null
            foreach (var provider in promotion.ShippingProviders)
            {
                provider.PromoCode = null;
                _context.Update(provider);
            }

            // Xóa promotion
            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Kiểm tra tên mã giảm giá có tồn tại không
        private async Task<bool> CheckPromoNameExists(string promoName)
        {
            if (string.IsNullOrWhiteSpace(promoName))
                return false;

            string normalized = promoName.Trim().ToLower();

            return await _context.Promotions
                .AsNoTracking()
                .AnyAsync(p => p.PromoName.ToLower() == normalized);
        }
    }
}