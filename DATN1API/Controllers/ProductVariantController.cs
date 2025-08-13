
using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductVariantsController : ControllerBase
    {
        private readonly DatnContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductVariantsController(DatnContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ========= GET all =========
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductVariant>>> GetProductVariants()
        {
            return await _context.ProductVariants
                                 .Include(v => v.Product)
                                 .Include(v => v.Color)
                                 .Include(v => v.Size)
                                 .ToListAsync();
        }

        // ========= GET by id =========
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductVariant>> GetProductVariant(int id)
        {
            var variant = await _context.ProductVariants
                                        .Include(v => v.Product)
                                        .Include(v => v.Color)
                                        .Include(v => v.Size)
                                        .FirstOrDefaultAsync(v => v.VariantId == id);
            if (variant == null) return NotFound();
            return variant;
        }

        // ========= GET by product =========
        [HttpGet("by-product/{productId:int}")]
        public async Task<ActionResult<IEnumerable<ProductVariant>>> GetByProduct(int productId)
        {
            var list = await _context.ProductVariants
                                     .Where(v => v.ProductId == productId)
                                     .Include(v => v.Product)
                                     .Include(v => v.Color)
                                     .Include(v => v.Size)
                                     .ToListAsync();
            return list;
        }

        // ========= POST create =========
        // ========= POST create =========
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateProductVariant(
            [FromForm] int ProductId,
            [FromForm] int ColorId,
            [FromForm] int SizeId,
            [FromForm] string? Sku,
            [FromForm] int? Stock,
            [FromForm] decimal? SalePrice,
            [FromForm] decimal? OriginalPrice,
            [FromForm] string? Status,
            [FromForm] IFormFile? ImageFile)
        {
            try
            {
                // Validate tham chiếu
                if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
                    return BadRequest("Sản phẩm không tồn tại!");
                if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
                    return BadRequest("Màu không tồn tại!");
                if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
                    return BadRequest("Size không tồn tại!");

                // Không cho trùng (ProductId, ColorId, SizeId)
                if (await _context.ProductVariants.AnyAsync(v =>
                        v.ProductId == ProductId && v.ColorId == ColorId && v.SizeId == SizeId))
                    return BadRequest("Biến thể với Sản phẩm - Màu - Size đã tồn tại!");

                // Không cho trùng SKU
                if (!string.IsNullOrWhiteSpace(Sku) &&
                    await _context.ProductVariants.AnyAsync(v => v.Sku == Sku))
                    return BadRequest("SKU đã tồn tại!");

                // Upload ảnh vào DATN1WEB/wwwroot/hinh (giống ProductController)
                string? thumbnailPath = null;
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var uploadPath = Path.Combine(_env.ContentRootPath, "..", "DATN1WEB", "wwwroot", "hinh");
                    Directory.CreateDirectory(uploadPath);

                    var safeName = Path.GetFileName(ImageFile.FileName);
                    var fileName = $"{Guid.NewGuid()}_{safeName}";
                    var filePath = Path.Combine(uploadPath, fileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await ImageFile.CopyToAsync(stream);

                    thumbnailPath = "/hinh/" + fileName; // ghi vào DB dạng /hinh/...
                }

                // Tự sinh SKU nếu bỏ trống (tránh unique index null)
                if (string.IsNullOrWhiteSpace(Sku))
                    Sku = $"BIG_V_{Guid.NewGuid().ToString()[..6]}";

                var variant = new ProductVariant
                {
                    ProductId = ProductId,
                    ColorId = ColorId,
                    SizeId = SizeId,
                    Sku = Sku,
                    Stock = Stock ?? 0,
                    SalePrice = SalePrice,
                    OriginalPrice = OriginalPrice,
                    Status = string.IsNullOrWhiteSpace(Status) ? "Active" : Status,
                    ThumbnailImage = thumbnailPath,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };

                _context.ProductVariants.Add(variant);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetProductVariant), new { id = variant.VariantId }, variant);
            }
            catch (DbUpdateException ex)
            {
                // trả thông báo rõ ràng (ví dụ lỗi unique index)
                return BadRequest("Không lưu được biến thể: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        // ========= PUT update =========
        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProductVariant(
            int id,
            [FromForm] int ProductId,
            [FromForm] int ColorId,
            [FromForm] int SizeId,
            [FromForm] string? Sku,
            [FromForm] int? Stock,
            [FromForm] decimal? SalePrice,
            [FromForm] decimal? OriginalPrice,
            [FromForm] string? Status,
            [FromForm] IFormFile? ImageFile)
        {
            try
            {
                var existing = await _context.ProductVariants.FindAsync(id);
                if (existing == null) return NotFound();

                if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
                    return BadRequest("Sản phẩm không tồn tại!");
                if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
                    return BadRequest("Màu không tồn tại!");
                if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
                    return BadRequest("Size không tồn tại!");

                if (!string.IsNullOrWhiteSpace(Sku) &&
                    await _context.ProductVariants.AnyAsync(v => v.Sku == Sku && v.VariantId != id))
                    return BadRequest("SKU đã tồn tại!");

                if (await _context.ProductVariants.AnyAsync(v =>
                        v.ProductId == ProductId && v.ColorId == ColorId && v.SizeId == SizeId && v.VariantId != id))
                    return BadRequest("Biến thể với Sản phẩm - Màu - Size này đã tồn tại!");

                existing.ProductId = ProductId;
                existing.ColorId = ColorId;
                existing.SizeId = SizeId;
                if (!string.IsNullOrWhiteSpace(Sku)) existing.Sku = Sku;
                existing.Stock = Stock ?? existing.Stock;
                existing.SalePrice = SalePrice;
                existing.OriginalPrice = OriginalPrice;
                if (!string.IsNullOrWhiteSpace(Status)) existing.Status = Status;
                existing.UpdatedDate = DateTime.UtcNow;

                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var uploadPath = Path.Combine(_env.ContentRootPath, "..", "DATN1WEB", "wwwroot", "hinh");
                    Directory.CreateDirectory(uploadPath);

                    var safeName = Path.GetFileName(ImageFile.FileName);
                    var fileName = $"{Guid.NewGuid()}_{safeName}";
                    var filePath = Path.Combine(uploadPath, fileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await ImageFile.CopyToAsync(stream);

                    existing.ThumbnailImage = "/hinh/" + fileName;
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException ex)
            {
                return BadRequest("Không cập nhật được biến thể: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        // ========= DELETE =========
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProductVariant(int id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);
            if (variant == null) return NotFound();

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
