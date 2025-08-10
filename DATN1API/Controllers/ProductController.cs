//using DATN1API.Data;
//using DATN1API.Models;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//namespace DATN1API.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class ProductController : ControllerBase
//    {
//        private readonly DatnContext _context;

//        public ProductController(DatnContext context)
//        {
//            _context = context;
//        }

//        // ================== Lấy tất cả sản phẩm ==================
//        [HttpGet]
//        public async Task<IActionResult> GetAll()
//        {
//            var products = await _context.Products
//                .Include(p => p.Category)
//                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
//                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
//                .ToListAsync();

//            return Ok(products);
//        }

//        // ================== Lấy sản phẩm theo ID ==================
//        [HttpGet("{id}")]
//        public async Task<IActionResult> GetById(int id)
//        {
//            var product = await _context.Products
//                .Include(p => p.Category)
//                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
//                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
//                .FirstOrDefaultAsync(p => p.ProductId == id);

//            if (product == null)
//                return NotFound(new { message = "Không tìm thấy sản phẩm" });

//            return Ok(product);
//        }

//        // ================== Tạo sản phẩm kèm biến thể ==================
//        //[HttpPost]
//        //public async Task<IActionResult> Create([FromForm] Product product, [FromForm] List<ProductVariant> variants)
//        //{
//        //    // Kiểm tra category
//        //    var categoryExists = await _context.Categories.AnyAsync(c => c.CategoryId == product.CategoryId);
//        //    if (!categoryExists)
//        //        return BadRequest(new { message = $"CategoryId {product.CategoryId} không tồn tại" });

//        //    // Xác định đường dẫn DATN1WEB/wwwroot/hinh
//        //    var webProjectPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, "DATN1WEB");
//        //    var uploadPath = Path.Combine(webProjectPath, "wwwroot", "hinh");
//        //    if (!Directory.Exists(uploadPath))
//        //        Directory.CreateDirectory(uploadPath);

//        //    // Upload ảnh nếu có
//        //    if (product.ImageFile != null)
//        //    {
//        //        var fileName = $"{Guid.NewGuid()}_{product.ImageFile.FileName}";
//        //        var filePath = Path.Combine(uploadPath, fileName);
//        //        using (var stream = new FileStream(filePath, FileMode.Create))
//        //        {
//        //            await product.ImageFile.CopyToAsync(stream);
//        //        }
//        //        product.ThumbnailImage = "/hinh/" + fileName;
//        //    }

//        //    product.CreatedDate = DateTime.UtcNow;
//        //    product.UpdatedDate = DateTime.UtcNow;

//        //    // Xử lý biến thể
//        //    foreach (var v in variants)
//        //    {
//        //        if (!await _context.Colors.AnyAsync(c => c.ColorId == v.ColorId))
//        //            return BadRequest(new { message = $"ColorId {v.ColorId} không tồn tại" });

//        //        if (!await _context.Sizes.AnyAsync(s => s.SizeId == v.SizeId))
//        //            return BadRequest(new { message = $"SizeId {v.SizeId} không tồn tại" });

//        //        v.Sku = Guid.NewGuid().ToString().Substring(0, 8);
//        //        v.CreatedDate = DateTime.UtcNow;
//        //        v.UpdatedDate = DateTime.UtcNow;
//        //        product.ProductVariants.Add(v);
//        //    }

//        //    _context.Products.Add(product);
//        //    await _context.SaveChangesAsync();

//        //    return Ok(new { message = "Tạo sản phẩm thành công", product });
//        //}
//        [HttpPost("CreateFull")]
//        public async Task<IActionResult> CreateFull([FromForm] Product product)
//        {
//            if (product.ProductVariants == null || !product.ProductVariants.Any())
//                return BadRequest(new { message = "Cần ít nhất một biến thể" });

//            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "hinh");
//            if (!Directory.Exists(uploadPath))
//                Directory.CreateDirectory(uploadPath);

//            // Upload ảnh sản phẩm chính
//            if (product.ImageFile != null)
//            {
//                var fileName = $"{Guid.NewGuid()}_{product.ImageFile.FileName}";
//                var filePath = Path.Combine(uploadPath, fileName);
//                using (var stream = new FileStream(filePath, FileMode.Create))
//                {
//                    await product.ImageFile.CopyToAsync(stream);
//                }
//                product.ThumbnailImage = "/hinh/" + fileName;
//            }

//            product.CreatedDate = DateTime.UtcNow;
//            product.UpdatedDate = DateTime.UtcNow;

//            var variants = product.ProductVariants.ToList();
//            product.ProductVariants.Clear();

//            _context.Products.Add(product);
//            await _context.SaveChangesAsync();

//            foreach (var variant in variants)
//            {
//                variant.ProductId = product.ProductId;
//                variant.Sku = $"BIG_SP_{Guid.NewGuid().ToString()[..6]}";
//                variant.CreatedDate = DateTime.UtcNow;
//                variant.UpdatedDate = DateTime.UtcNow;

//                if (variant.ImageFile != null)
//                {
//                    var vFileName = $"{Guid.NewGuid()}_{variant.ImageFile.FileName}";
//                    var vFilePath = Path.Combine(uploadPath, vFileName);
//                    using (var stream = new FileStream(vFilePath, FileMode.Create))
//                    {
//                        await variant.ImageFile.CopyToAsync(stream);
//                    }
//                    variant.ThumbnailImage = "/hinh/" + vFileName;
//                }

//                _context.ProductVariants.Add(variant);
//            }

//            await _context.SaveChangesAsync();
//            return Ok(new { message = "Tạo sản phẩm và biến thể thành công", product });
//        }


//        // ================== Cập nhật sản phẩm kèm biến thể ==================
//        [HttpPut("{id}")]
//        public async Task<IActionResult> Update(int id, [FromForm] Product product, [FromForm] List<ProductVariant> variants)
//        {
//            var dbProduct = await _context.Products
//                .Include(p => p.ProductVariants)
//                .FirstOrDefaultAsync(p => p.ProductId == id);

//            if (dbProduct == null)
//                return NotFound(new { message = "Không tìm thấy sản phẩm" });

//            if (!await _context.Categories.AnyAsync(c => c.CategoryId == product.CategoryId))
//                return BadRequest(new { message = $"CategoryId {product.CategoryId} không tồn tại" });

//            dbProduct.ProductName = product.ProductName;
//            dbProduct.Description = product.Description;
//            dbProduct.SalePrice = product.SalePrice;
//            dbProduct.OriginalPrice = product.OriginalPrice;
//            dbProduct.Material = product.Material;
//            dbProduct.CategoryId = product.CategoryId;
//            dbProduct.Status = product.Status;
//            dbProduct.UpdatedDate = DateTime.UtcNow;

//            // Xác định đường dẫn DATN1WEB/wwwroot/hinh
//            var webProjectPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, "DATN1WEB");
//            var uploadPath = Path.Combine(webProjectPath, "wwwroot", "hinh");
//            if (!Directory.Exists(uploadPath))
//                Directory.CreateDirectory(uploadPath);

//            // Upload ảnh mới nếu có
//            if (product.ImageFile != null)
//            {
//                var fileName = $"{Guid.NewGuid()}_{product.ImageFile.FileName}";
//                var filePath = Path.Combine(uploadPath, fileName);
//                using (var stream = new FileStream(filePath, FileMode.Create))
//                {
//                    await product.ImageFile.CopyToAsync(stream);
//                }
//                dbProduct.ThumbnailImage = "/hinh/" + fileName;
//            }

//            // Xóa biến thể cũ và thêm mới
//            _context.ProductVariants.RemoveRange(dbProduct.ProductVariants);
//            foreach (var v in variants)
//            {
//                if (!await _context.Colors.AnyAsync(c => c.ColorId == v.ColorId))
//                    return BadRequest(new { message = $"ColorId {v.ColorId} không tồn tại" });

//                if (!await _context.Sizes.AnyAsync(s => s.SizeId == v.SizeId))
//                    return BadRequest(new { message = $"SizeId {v.SizeId} không tồn tại" });

//                v.Sku = Guid.NewGuid().ToString().Substring(0, 8);
//                v.CreatedDate = DateTime.UtcNow;
//                v.UpdatedDate = DateTime.UtcNow;
//                dbProduct.ProductVariants.Add(v);
//            }

//            await _context.SaveChangesAsync();
//            return Ok(new { message = "Cập nhật sản phẩm thành công" });
//        }

//        // ================== Xóa sản phẩm ==================
//        [HttpDelete("{id}")]
//        public async Task<IActionResult> Delete(int id)
//        {
//            var product = await _context.Products
//                .Include(p => p.ProductVariants)
//                .FirstOrDefaultAsync(p => p.ProductId == id);

//            if (product == null)
//                return NotFound(new { message = "Không tìm thấy sản phẩm" });

//            _context.ProductVariants.RemoveRange(product.ProductVariants);
//            _context.Products.Remove(product);
//            await _context.SaveChangesAsync();

//            return Ok(new { message = "Xoá sản phẩm thành công" });
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
    public class ProductController : ControllerBase
    {
        private readonly DatnContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductController(DatnContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ================== Lấy tất cả sản phẩm ==================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .ToListAsync();

            return Ok(products);
        }

        // ================== Lấy sản phẩm theo ID ==================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Color)
                .Include(p => p.ProductVariants).ThenInclude(v => v.Size)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound(new { message = "Không tìm thấy sản phẩm" });

            return Ok(product);
        }

        // ================== Tạo sản phẩm kèm biến thể ==================
        [HttpPost("CreateFull")]
        public async Task<IActionResult> CreateFull([FromForm] Product product)
        {
            if (product.ProductVariants == null || !product.ProductVariants.Any())
                return BadRequest(new { message = "Cần ít nhất một biến thể" });

            var uploadPath = Path.Combine(_env.ContentRootPath, "..", "DATN1WEB", "wwwroot", "hinh");
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            // Upload ảnh sản phẩm chính
            if (product.ImageFile != null)
            {
                var fileName = $"{Guid.NewGuid()}_{product.ImageFile.FileName}";
                var filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await product.ImageFile.CopyToAsync(stream);
                }
                product.ThumbnailImage = "/hinh/" + fileName;
            }

            product.CreatedDate = DateTime.UtcNow;
            product.UpdatedDate = DateTime.UtcNow;

            var variants = product.ProductVariants.ToList();
            product.ProductVariants.Clear();

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Lưu biến thể
            foreach (var variant in variants)
            {
                variant.ProductId = product.ProductId;
                variant.Sku = $"BIG_SP_{Guid.NewGuid().ToString()[..6]}";
                variant.CreatedDate = DateTime.UtcNow;
                variant.UpdatedDate = DateTime.UtcNow;

                if (variant.ImageFile != null)
                {
                    var vFileName = $"{Guid.NewGuid()}_{variant.ImageFile.FileName}";
                    var vFilePath = Path.Combine(uploadPath, vFileName);
                    using (var stream = new FileStream(vFilePath, FileMode.Create))
                    {
                        await variant.ImageFile.CopyToAsync(stream);
                    }
                    variant.ThumbnailImage = "/hinh/" + vFileName;
                }

                _context.ProductVariants.Add(variant);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Tạo sản phẩm và biến thể thành công", product });
        }

        // ================== Cập nhật sản phẩm kèm biến thể ==================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] Product product)
        {
            var dbProduct = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (dbProduct == null)
                return NotFound(new { message = "Không tìm thấy sản phẩm" });

            if (!await _context.Categories.AnyAsync(c => c.CategoryId == product.CategoryId))
                return BadRequest(new { message = $"CategoryId {product.CategoryId} không tồn tại" });

            dbProduct.ProductName = product.ProductName;
            dbProduct.Description = product.Description;
            dbProduct.SalePrice = product.SalePrice;
            dbProduct.OriginalPrice = product.OriginalPrice;
            dbProduct.Material = product.Material;
            dbProduct.CategoryId = product.CategoryId;
            dbProduct.Status = product.Status;
            dbProduct.UpdatedDate = DateTime.UtcNow;

            var uploadPath = Path.Combine(_env.ContentRootPath, "..", "DATN1WEB", "wwwroot", "hinh");
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            // Upload ảnh mới nếu có
            if (product.ImageFile != null)
            {
                var fileName = $"{Guid.NewGuid()}_{product.ImageFile.FileName}";
                var filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await product.ImageFile.CopyToAsync(stream);
                }
                dbProduct.ThumbnailImage = "/hinh/" + fileName;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật sản phẩm thành công" });
        }


        // ================== Xóa sản phẩm ==================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductVariants)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound(new { message = "Không tìm thấy sản phẩm" });

            _context.ProductVariants.RemoveRange(product.ProductVariants);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xoá sản phẩm thành công" });
        }
    }
}
