
//using DATN1API.Data;
//using DATN1API.Models;
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

//        // ========= GET all =========
//        [HttpGet]
//        public async Task<ActionResult<IEnumerable<ProductVariant>>> GetProductVariants()
//        {
//            return await _context.ProductVariants
//                                 .Include(v => v.Product)
//                                 .Include(v => v.Color)
//                                 .Include(v => v.Size)
//                                 .ToListAsync();
//        }

//        // ========= GET by id =========
//        [HttpGet("{id:int}")]
//        public async Task<ActionResult<ProductVariant>> GetProductVariant(int id)
//        {
//            var variant = await _context.ProductVariants
//                                        .Include(v => v.Product)
//                                        .Include(v => v.Color)
//                                        .Include(v => v.Size)
//                                        .FirstOrDefaultAsync(v => v.VariantId == id);
//            if (variant == null) return NotFound();
//            return variant;
//        }

//        // ========= GET by product =========
//        [HttpGet("by-product/{productId:int}")]
//        public async Task<ActionResult<IEnumerable<ProductVariant>>> GetByProduct(int productId)
//        {
//            var list = await _context.ProductVariants
//                                     .Where(v => v.ProductId == productId)
//                                     .Include(v => v.Product)
//                                     .Include(v => v.Color)
//                                     .Include(v => v.Size)
//                                     .ToListAsync();
//            return list;
//        }

//        // ========= POST create =========
//        // ========= POST create =========
//        [HttpPost]
//        [Consumes("multipart/form-data")]
//        public async Task<IActionResult> CreateProductVariant(
//            [FromForm] int ProductId,
//            [FromForm] int ColorId,
//            [FromForm] int SizeId,
//            [FromForm] string? Sku,
//            [FromForm] int? Stock,
//            [FromForm] decimal? SalePrice,
//            [FromForm] decimal? OriginalPrice,
//            [FromForm] string? Status,
//            [FromForm] IFormFile? ImageFile)
//        {
//            try
//            {
//                // Validate tham chiếu
//                if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
//                    return BadRequest("Sản phẩm không tồn tại!");
//                if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
//                    return BadRequest("Màu không tồn tại!");
//                if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
//                    return BadRequest("Size không tồn tại!");

//                // Không cho trùng (ProductId, ColorId, SizeId)
//                if (await _context.ProductVariants.AnyAsync(v =>
//                        v.ProductId == ProductId && v.ColorId == ColorId && v.SizeId == SizeId))
//                    return BadRequest("Biến thể với Sản phẩm - Màu - Size đã tồn tại!");

//                // Không cho trùng SKU
//                if (!string.IsNullOrWhiteSpace(Sku) &&
//                    await _context.ProductVariants.AnyAsync(v => v.Sku == Sku))
//                    return BadRequest("SKU đã tồn tại!");

//                // Upload ảnh vào DATN1WEB/wwwroot/hinh (giống ProductController)
//                string? thumbnailPath = null;
//                if (ImageFile != null && ImageFile.Length > 0)
//                {
//                    var uploadPath = Path.Combine(_env.ContentRootPath, "..", "DATN1WEB", "wwwroot", "hinh");
//                    Directory.CreateDirectory(uploadPath);

//                    var safeName = Path.GetFileName(ImageFile.FileName);
//                    var fileName = $"{Guid.NewGuid()}_{safeName}";
//                    var filePath = Path.Combine(uploadPath, fileName);
//                    using var stream = new FileStream(filePath, FileMode.Create);
//                    await ImageFile.CopyToAsync(stream);

//                    thumbnailPath = "/hinh/" + fileName; // ghi vào DB dạng /hinh/...
//                }

//                // Tự sinh SKU nếu bỏ trống (tránh unique index null)
//                if (string.IsNullOrWhiteSpace(Sku))
//                    Sku = $"BIG_V_{Guid.NewGuid().ToString()[..6]}";

//                var variant = new ProductVariant
//                {
//                    ProductId = ProductId,
//                    ColorId = ColorId,
//                    SizeId = SizeId,
//                    Sku = Sku,
//                    Stock = Stock ?? 0,
//                    SalePrice = SalePrice,
//                    OriginalPrice = OriginalPrice,
//                    Status = string.IsNullOrWhiteSpace(Status) ? "Active" : Status,
//                    ThumbnailImage = thumbnailPath,
//                    CreatedDate = DateTime.UtcNow,
//                    UpdatedDate = DateTime.UtcNow
//                };

//                _context.ProductVariants.Add(variant);
//                await _context.SaveChangesAsync();

//                return CreatedAtAction(nameof(GetProductVariant), new { id = variant.VariantId }, variant);
//            }
//            catch (DbUpdateException ex)
//            {
//                // trả thông báo rõ ràng (ví dụ lỗi unique index)
//                return BadRequest("Không lưu được biến thể: " + (ex.InnerException?.Message ?? ex.Message));
//            }
//        }

//        // ========= PUT update =========
//        [HttpPut("{id:int}")]
//        [Consumes("multipart/form-data")]
//        public async Task<IActionResult> UpdateProductVariant(
//            int id,
//            [FromForm] int ProductId,
//            [FromForm] int ColorId,
//            [FromForm] int SizeId,
//            [FromForm] string? Sku,
//            [FromForm] int? Stock,
//            [FromForm] decimal? SalePrice,
//            [FromForm] decimal? OriginalPrice,
//            [FromForm] string? Status,
//            [FromForm] IFormFile? ImageFile)
//        {
//            try
//            {
//                var existing = await _context.ProductVariants.FindAsync(id);
//                if (existing == null) return NotFound();

//                if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
//                    return BadRequest("Sản phẩm không tồn tại!");
//                if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
//                    return BadRequest("Màu không tồn tại!");
//                if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
//                    return BadRequest("Size không tồn tại!");

//                if (!string.IsNullOrWhiteSpace(Sku) &&
//                    await _context.ProductVariants.AnyAsync(v => v.Sku == Sku && v.VariantId != id))
//                    return BadRequest("SKU đã tồn tại!");

//                if (await _context.ProductVariants.AnyAsync(v =>
//                        v.ProductId == ProductId && v.ColorId == ColorId && v.SizeId == SizeId && v.VariantId != id))
//                    return BadRequest("Biến thể với Sản phẩm - Màu - Size này đã tồn tại!");

//                existing.ProductId = ProductId;
//                existing.ColorId = ColorId;
//                existing.SizeId = SizeId;
//                if (!string.IsNullOrWhiteSpace(Sku)) existing.Sku = Sku;
//                existing.Stock = Stock ?? existing.Stock;
//                existing.SalePrice = SalePrice;
//                existing.OriginalPrice = OriginalPrice;
//                if (!string.IsNullOrWhiteSpace(Status)) existing.Status = Status;
//                existing.UpdatedDate = DateTime.UtcNow;

//                if (ImageFile != null && ImageFile.Length > 0)
//                {
//                    var uploadPath = Path.Combine(_env.ContentRootPath, "..", "DATN1WEB", "wwwroot", "hinh");
//                    Directory.CreateDirectory(uploadPath);

//                    var safeName = Path.GetFileName(ImageFile.FileName);
//                    var fileName = $"{Guid.NewGuid()}_{safeName}";
//                    var filePath = Path.Combine(uploadPath, fileName);
//                    using var stream = new FileStream(filePath, FileMode.Create);
//                    await ImageFile.CopyToAsync(stream);

//                    existing.ThumbnailImage = "/hinh/" + fileName;
//                }

//                await _context.SaveChangesAsync();
//                return NoContent();
//            }
//            catch (DbUpdateException ex)
//            {
//                return BadRequest("Không cập nhật được biến thể: " + (ex.InnerException?.Message ?? ex.Message));
//            }
//        }

//        // ========= DELETE =========
//        [HttpDelete("{id:int}")]
//        public async Task<IActionResult> DeleteProductVariant(int id)
//        {
//            var variant = await _context.ProductVariants.FindAsync(id);
//            if (variant == null) return NotFound();

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
        private readonly ILogger<ProductVariantsController> _logger;

        public ProductVariantsController(
            DatnContext context,
            IWebHostEnvironment env,
            ILogger<ProductVariantsController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        // ================= Helpers =================

        // .../DATN1WEB/wwwroot/hinh (absolute)
        private string ResolveWebHinhPath()
        {
            var start = new DirectoryInfo(_env.ContentRootPath);
            DirectoryInfo? cur = start;

            while (cur != null)
            {
                var webDir = Path.Combine(cur.FullName, "DATN1WEB");
                if (Directory.Exists(webDir))
                {
                    var hinh = Path.Combine(webDir, "wwwroot", "hinh");
                    Directory.CreateDirectory(hinh);
                    return hinh;
                }
                cur = cur.Parent;
            }

            // fallback (dev)
            var fallback = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "DATN1WEB", "wwwroot", "hinh"));
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private string ResolveVariantGalleryAbsDir(int variantId)
        {
            var baseHinh = ResolveWebHinhPath();
            var dir = Path.Combine(baseHinh, "variants", variantId.ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }

        // save ONE image to /hinh
        private async Task<string?> SaveImageToWebAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var hinhAbs = ResolveWebHinhPath();
            var ext = Path.GetExtension(Path.GetFileName(file.FileName));
            var name = $"{Guid.NewGuid():N}{ext}";
            var abs = Path.Combine(hinhAbs, name);

            _logger.LogInformation("Saving variant main image => {AbsPath}", abs);
            await using var fs = new FileStream(abs, FileMode.Create);
            await file.CopyToAsync(fs);

            return "/hinh/" + name;
        }

        // save MANY gallery images
        private async Task SaveGalleryFilesAsync(int variantId, IEnumerable<IFormFile> files)
        {
            var dir = ResolveVariantGalleryAbsDir(variantId);
            foreach (var f in files.Where(f => f?.Length > 0))
            {
                var safe = Path.GetFileName(f.FileName);
                var name = $"{Guid.NewGuid():N}_{safe}";
                var abs = Path.Combine(dir, name);
                _logger.LogInformation("Saving gallery image => {Abs}", abs);
                await using var fs = new FileStream(abs, FileMode.Create);
                await f.CopyToAsync(fs);
            }
        }

        // load gallery list for a variant
        private List<string> BuildGalleryList(int variantId)
        {
            var dir = ResolveVariantGalleryAbsDir(variantId);
            if (!Directory.Exists(dir)) return new();
            return Directory.GetFiles(dir)
                            .Select(p => "/hinh/variants/" + variantId + "/" + Path.GetFileName(p))
                            .ToList();
        }

        // ================== GET ==================

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductVariant>>> GetProductVariants()
        {
            var list = await _context.ProductVariants
                                     .Include(v => v.Product)
                                     .Include(v => v.Color)
                                     .Include(v => v.Size)
                                     .ToListAsync();
            return list;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductVariant>> GetProductVariant(int id)
        {
            var variant = await _context.ProductVariants
                                        .Include(v => v.Product)
                                        .Include(v => v.Color)
                                        .Include(v => v.Size)
                                        .FirstOrDefaultAsync(v => v.VariantId == id);
            if (variant == null) return NotFound();

            variant.GalleryPaths = BuildGalleryList(id);
            return variant;
        }

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

        // ================== CREATE ==================

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
            [FromForm] IFormFile? ImageFile,               // thumbnail
            [FromForm] List<IFormFile>? GalleryFiles)      // many gallery images
        {
            try
            {
                // validate FK & unique
                if (!await _context.Products.AnyAsync(p => p.ProductId == ProductId))
                    return BadRequest("Sản phẩm không tồn tại!");
                if (!await _context.Colors.AnyAsync(c => c.ColorId == ColorId))
                    return BadRequest("Màu không tồn tại!");
                if (!await _context.Sizes.AnyAsync(s => s.SizeId == SizeId))
                    return BadRequest("Size không tồn tại!");

                if (await _context.ProductVariants.AnyAsync(v =>
                        v.ProductId == ProductId && v.ColorId == ColorId && v.SizeId == SizeId))
                    return BadRequest("Biến thể với Sản phẩm - Màu - Size đã tồn tại!");

                if (!string.IsNullOrWhiteSpace(Sku) &&
                    await _context.ProductVariants.AnyAsync(v => v.Sku == Sku))
                    return BadRequest("SKU đã tồn tại!");

                string? thumbnailPath = null;
                try
                {
                    thumbnailPath = await SaveImageToWebAsync(ImageFile);
                }
                catch (Exception ioEx)
                {
                    _logger.LogError(ioEx, "Save image failed");
                    return StatusCode(500, "Không lưu được ảnh biến thể vào thư mục web/hinh.");
                }

                if (string.IsNullOrWhiteSpace(Sku))
                    Sku = $"BIG_V_{Guid.NewGuid():N}".Substring(0, 12);

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
                await _context.SaveChangesAsync(); // need VariantId

                if (GalleryFiles is { Count: > 0 })
                {
                    try { await SaveGalleryFilesAsync(variant.VariantId, GalleryFiles); }
                    catch (Exception galEx) { _logger.LogError(galEx, "Save gallery failed"); }
                }

                variant.GalleryPaths = BuildGalleryList(variant.VariantId);
                return CreatedAtAction(nameof(GetProductVariant), new { id = variant.VariantId }, variant);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DB update error");
                return BadRequest("Không lưu được biến thể: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        // ================== UPDATE ==================

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
            [FromForm] IFormFile? ImageFile,               // new thumbnail (optional)
            [FromForm] List<IFormFile>? GalleryFiles,      // add new gallery images
            [FromForm] List<string>? RemoveGallery)        // file names to delete
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

                // update fields
                existing.ProductId = ProductId;
                existing.ColorId = ColorId;
                existing.SizeId = SizeId;
                if (!string.IsNullOrWhiteSpace(Sku)) existing.Sku = Sku;
                existing.Stock = Stock ?? existing.Stock;
                existing.SalePrice = SalePrice;
                existing.OriginalPrice = OriginalPrice;
                if (!string.IsNullOrWhiteSpace(Status)) existing.Status = Status;
                existing.UpdatedDate = DateTime.UtcNow;

                // new thumbnail
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    try { existing.ThumbnailImage = await SaveImageToWebAsync(ImageFile); }
                    catch (Exception ioEx)
                    {
                        _logger.LogError(ioEx, "Save image (update) failed");
                        return StatusCode(500, "Không lưu được ảnh biến thể vào thư mục web/hinh.");
                    }
                }

                // add gallery images
                if (GalleryFiles is { Count: > 0 })
                    await SaveGalleryFilesAsync(id, GalleryFiles);

                // delete gallery images by file name
                if (RemoveGallery is { Count: > 0 })
                {
                    var dir = ResolveVariantGalleryAbsDir(id);
                    foreach (var fileName in RemoveGallery)
                    {
                        var safe = Path.GetFileName(fileName);
                        var full = Path.Combine(dir, safe);
                        if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                    }
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DB update error");
                return BadRequest("Không cập nhật được biến thể: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        // ================== DELETE ==================

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProductVariant(int id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);
            if (variant == null) return NotFound();

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            // (optional) delete gallery folder
            try
            {
                var dir = ResolveVariantGalleryAbsDir(id);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { /* ignore */ }

            return NoContent();
        }
    }
}

