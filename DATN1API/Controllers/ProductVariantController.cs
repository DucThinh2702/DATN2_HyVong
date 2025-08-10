//using DATN1API.Data;
//using DATN1API.Models;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//namespace DATN1API.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class ProductVariantsController : ControllerBase
//    {
//        private readonly DatnContext _context;
//        private readonly IWebHostEnvironment _env;

//        public ProductVariantsController(DatnContext context, IWebHostEnvironment env)
//        {
//            _context = context;
//            _env = env;
//        }

//        // GET: api/ProductVariants
//        [HttpGet]
//        public async Task<ActionResult<IEnumerable<ProductVariant>>> GetProductVariants()
//        {
//            return await _context.ProductVariants.ToListAsync();
//        }

//        // GET: api/ProductVariants/5
//        [HttpGet("{id}")]
//        public async Task<ActionResult<ProductVariant>> GetProductVariant(int id)
//        {
//            var variant = await _context.ProductVariants.FindAsync(id);
//            if (variant == null)
//                return NotFound();

//            return variant;
//        }

//        // POST: api/ProductVariants
//        [HttpPost]
//        public async Task<IActionResult> CreateProductVariant(
//            [FromForm] int ProductId,
//            [FromForm] int ColorId,
//            [FromForm] int SizeId,
//            [FromForm] string Sku,
//            [FromForm] int Stock,
//            [FromForm] decimal SalePrice,
//            [FromForm] decimal OriginalPrice,
//            [FromForm] string Status,
//            [FromForm] IFormFile? ImageFile)
//        {
//            // Kiểm tra tồn tại
//            if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
//                return BadRequest("Sản phẩm không tồn tại!");
//            if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
//                return BadRequest("Màu không tồn tại!");
//            if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
//                return BadRequest("Size không tồn tại!");
//            if (await _context.ProductVariants.AnyAsync(v => v.Sku == Sku))
//                return BadRequest("SKU đã tồn tại!");

//            string? thumbnailPath = null;
//            if (ImageFile != null && ImageFile.Length > 0)
//            {
//                var fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
//                var savePath = Path.Combine(_env.WebRootPath, "hinh", fileName);
//                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

//                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
//                using (var stream = new FileStream(savePath, FileMode.Create))
//                {
//                    await ImageFile.CopyToAsync(stream);
//                }
//                thumbnailPath = "/hinh/" + fileName;

//            }

//            var variant = new ProductVariant
//            {
//                ProductId = ProductId,
//                ColorId = ColorId,
//                SizeId = SizeId,
//                Sku = Sku,
//                Stock = Stock,
//                SalePrice = SalePrice,
//                OriginalPrice = OriginalPrice,
//                Status = Status,
//                ThumbnailImage = thumbnailPath,
//                CreatedDate = DateTime.Now,
//                UpdatedDate = DateTime.Now
//            };

//            _context.ProductVariants.Add(variant);
//            await _context.SaveChangesAsync();

//            return CreatedAtAction(nameof(GetProductVariant), new { id = variant.VariantId }, variant);
//        }

//        // PUT: api/ProductVariants/5
//        [HttpPut("{id}")]
//        public async Task<IActionResult> UpdateProductVariant(
//            int id,
//            [FromForm] int ProductId,
//            [FromForm] int ColorId,
//            [FromForm] int SizeId,
//            [FromForm] string Sku,
//            [FromForm] int Stock,
//            [FromForm] decimal SalePrice,
//            [FromForm] decimal OriginalPrice,
//            [FromForm] string Status,
//            [FromForm] IFormFile? ImageFile)
//        {
//            var existing = await _context.ProductVariants.FindAsync(id);
//            if (existing == null)
//                return NotFound();

//            if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
//                return BadRequest("Sản phẩm không tồn tại!");
//            if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
//                return BadRequest("Màu không tồn tại!");
//            if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
//                return BadRequest("Size không tồn tại!");
//            if (await _context.ProductVariants.AnyAsync(v => v.Sku == Sku && v.VariantId != id))
//                return BadRequest("SKU đã tồn tại!");

//            existing.ProductId = ProductId;
//            existing.ColorId = ColorId;
//            existing.SizeId = SizeId;
//            existing.Sku = Sku;
//            existing.Stock = Stock;
//            existing.SalePrice = SalePrice;
//            existing.OriginalPrice = OriginalPrice;
//            existing.Status = Status;
//            existing.UpdatedDate = DateTime.Now;

//            if (ImageFile != null && ImageFile.Length > 0)
//            {
//                var fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
//                var savePath = Path.Combine(_env.WebRootPath, "uploads", fileName);
//                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
//                using (var stream = new FileStream(savePath, FileMode.Create))
//                {
//                    await ImageFile.CopyToAsync(stream);
//                }
//                existing.ThumbnailImage = "/uploads/" + fileName;
//            }

//            _context.ProductVariants.Update(existing);
//            await _context.SaveChangesAsync();

//            return NoContent();
//        }

//        // DELETE: api/ProductVariants/5
//        [HttpDelete("{id}")]
//        public async Task<IActionResult> DeleteProductVariant(int id)
//        {
//            var variant = await _context.ProductVariants.FindAsync(id);
//            if (variant == null)
//                return NotFound();

//            _context.ProductVariants.Remove(variant);
//            await _context.SaveChangesAsync();

//            return NoContent();
//        }
//    }
//}
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

        // GET: api/ProductVariants
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductVariant>>> GetProductVariants()
        {
            return await _context.ProductVariants.ToListAsync();
        }

        // GET: api/ProductVariants/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductVariant>> GetProductVariant(int id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);
            if (variant == null)
                return NotFound();

            return variant;
        }

        // POST: api/ProductVariants
        [HttpPost]
        public async Task<IActionResult> CreateProductVariant(
            [FromForm] int ProductId,
            [FromForm] int ColorId,
            [FromForm] int SizeId,
            [FromForm] string Sku,
            [FromForm] int Stock,
            [FromForm] decimal SalePrice,
            [FromForm] decimal OriginalPrice,
            [FromForm] string Status,
            [FromForm] IFormFile? ImageFile)
        {
            // Kiểm tra tồn tại
            if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
                return BadRequest("Sản phẩm không tồn tại!");
            if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
                return BadRequest("Màu không tồn tại!");
            if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
                return BadRequest("Size không tồn tại!");
            if (await _context.ProductVariants.AnyAsync(v => v.Sku == Sku))
                return BadRequest("SKU đã tồn tại!");

            // Tạo đường dẫn lưu ảnh
            string? thumbnailPath = null;
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadPath = Path.Combine(_env.ContentRootPath, "..", "DATN1WEB", "wwwroot", "hinh");
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                var savePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(stream);
                }
                thumbnailPath = "/hinh/" + fileName;
            }

            var variant = new ProductVariant
            {
                ProductId = ProductId,
                ColorId = ColorId,
                SizeId = SizeId,
                Sku = Sku,
                Stock = Stock,
                SalePrice = SalePrice,
                OriginalPrice = OriginalPrice,
                Status = Status,
                ThumbnailImage = thumbnailPath,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProductVariant), new { id = variant.VariantId }, variant);
        }

        // PUT: api/ProductVariants/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProductVariant(
            int id,
            [FromForm] int ProductId,
            [FromForm] int ColorId,
            [FromForm] int SizeId,
            [FromForm] string Sku,
            [FromForm] int Stock,
            [FromForm] decimal SalePrice,
            [FromForm] decimal OriginalPrice,
            [FromForm] string Status,
            [FromForm] IFormFile? ImageFile)
        {
            var existing = await _context.ProductVariants.FindAsync(id);
            if (existing == null)
                return NotFound();

            if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
                return BadRequest("Sản phẩm không tồn tại!");
            if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
                return BadRequest("Màu không tồn tại!");
            if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
                return BadRequest("Size không tồn tại!");
            if (await _context.ProductVariants.AnyAsync(v => v.Sku == Sku && v.VariantId != id))
                return BadRequest("SKU đã tồn tại!");

            existing.ProductId = ProductId;
            existing.ColorId = ColorId;
            existing.SizeId = SizeId;
            existing.Sku = Sku;
            existing.Stock = Stock;
            existing.SalePrice = SalePrice;
            existing.OriginalPrice = OriginalPrice;
            existing.Status = Status;
            existing.UpdatedDate = DateTime.Now;

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadPath = Path.Combine(_env.WebRootPath, "hinh");
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(ImageFile.FileName)}";
                var savePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(stream);
                }
                existing.ThumbnailImage = "/hinh/" + fileName;
            }

            _context.ProductVariants.Update(existing);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/ProductVariants/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductVariant(int id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);
            if (variant == null)
                return NotFound();

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
