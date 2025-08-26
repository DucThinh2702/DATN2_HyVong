using DATN1API.Models;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Headers;

namespace DATN1WEB.Controllers
{
    public class ProductVariantsController : Controller
    {
        private readonly HttpClient _client;
        private readonly ILogger<ProductVariantsController> _logger;

        public ProductVariantsController(IHttpClientFactory httpClientFactory, ILogger<ProductVariantsController> logger)
        {
            _client = httpClientFactory.CreateClient("api");
            _logger = logger;
        }

        // ============== INDEX (giữ nguyên tuỳ bạn) ==============
        public async Task<IActionResult> Index(int? productId)
        {
            var url = "api/ProductVariants" + (productId.HasValue ? $"/by-product/{productId.Value}" : "");
            var variants = await _client.GetFromJsonAsync<List<ProductVariant>>(url) ?? new();
            ViewBag.ProductId = productId;
            return View(variants);
        }

        // ============== DETAILS (trả về GalleryPaths) ==============
        public async Task<IActionResult> Details(int id)
        {
            var variant = await _client.GetFromJsonAsync<ProductVariant>($"api/ProductVariants/{id}");
            if (variant == null) return NotFound();
            return View(variant);
        }

        // ============== CREATE ==============
        [HttpGet]
        public async Task<IActionResult> Create(int? productId)
        {
            await LoadProductColorSize(productId);
            return View(new ProductVariant { ProductId = productId ?? 0, Status = "Active" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductVariant variant)
        {
            // Bắt buộc sinh SKU server-side
            variant.Sku = $"BAG_{variant.SizeId}_{variant.ColorId}_{variant.ProductId}";

            if (!ModelState.IsValid)
            {
                await LoadProductColorSize(variant.ProductId);
                return View(variant);
            }
            if (variant.ProductId <= 0) ModelState.AddModelError("ProductId", "Vui lòng chọn sản phẩm");
            if (variant.ColorId <= 0) ModelState.AddModelError("ColorId", "Vui lòng chọn màu sắc");
            if (variant.SizeId <= 0) ModelState.AddModelError("SizeId", "Vui lòng chọn kích cỡ");
            if (!ModelState.IsValid)
            {
                await LoadProductColorSize(variant.ProductId);
                return View(variant);
            }

            try
            {
                var formData = new MultipartFormDataContent
                {
                    { new StringContent(variant.ProductId.ToString()), "ProductId" },
                    { new StringContent(variant.ColorId.ToString()),   "ColorId"   },
                    { new StringContent(variant.SizeId.ToString()),    "SizeId"    },
                    { new StringContent(variant.Sku ?? string.Empty),  "Sku"       },
                    { new StringContent((variant.Stock ?? 0).ToString(CultureInfo.InvariantCulture)), "Stock" },
                    { new StringContent((variant.SalePrice ?? 0).ToString(CultureInfo.InvariantCulture)), "SalePrice" },
                    { new StringContent((variant.OriginalPrice ?? 0).ToString(CultureInfo.InvariantCulture)), "OriginalPrice" },
                    { new StringContent(string.IsNullOrWhiteSpace(variant.Status) ? "Active" : variant.Status!), "Status" }
                };

                // thumbnail
                var main = variant.ImageFile ?? Request.Form.Files.FirstOrDefault(f => f.Name == "ImageFile");
                if (main is { Length: > 0 })
                {
                    var content = new StreamContent(main.OpenReadStream());
                    content.Headers.ContentLength = main.Length;
                    content.Headers.ContentType = new MediaTypeHeaderValue(main.ContentType);
                    formData.Add(content, "ImageFile", main.FileName);
                }

                // gallery (multiple)
                foreach (var gf in Request.Form.Files.Where(f => f.Name == "GalleryFiles" && f.Length > 0))
                {
                    var sc = new StreamContent(gf.OpenReadStream());
                    sc.Headers.ContentLength = gf.Length;
                    sc.Headers.ContentType = new MediaTypeHeaderValue(gf.ContentType);
                    formData.Add(sc, "GalleryFiles", gf.FileName); // same key many times
                }

                var resp = await _client.PostAsync("api/ProductVariants", formData);
                if (resp.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Thêm biến thể thành công!";
                    return RedirectToAction("Details", "SanPham", new { id = variant.ProductId });
                }

                ModelState.AddModelError("", "Lỗi khi thêm: " + await resp.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create variant error");
                ModelState.AddModelError("", "Có lỗi xảy ra khi thêm biến thể.");
            }

            await LoadProductColorSize(variant.ProductId);
            return View(variant);
        }

        // ============== EDIT ==============
        [HttpGet]
        public async Task<IActionResult> Edit(int id, int? productId)
        {
            var variant = await _client.GetFromJsonAsync<ProductVariant>($"api/ProductVariants/{id}");
            if (variant == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy biến thể.";
                return productId.HasValue
                    ? RedirectToAction("Details", "SanPham", new { id = productId.Value })
                    : RedirectToAction(nameof(Index));
            }

            await LoadProductColorSize(variant.ProductId);
            return View(variant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ProductVariant variant,
            IFormFile? ImageFile,
            List<IFormFile>? GalleryFiles,
            string[]? RemoveGallery)
        {
            if (id != variant.VariantId) return BadRequest();

            if (!ModelState.IsValid)
            {
                await LoadProductColorSize(variant.ProductId);
                return View(variant);
            }

            try
            {
                var formData = new MultipartFormDataContent
                {
                    { new StringContent(variant.ProductId.ToString()), "ProductId" },
                    { new StringContent(variant.ColorId.ToString()),   "ColorId"   },
                    { new StringContent(variant.SizeId.ToString()),    "SizeId"    },
                    { new StringContent(variant.Sku ?? string.Empty),  "Sku"       },
                    { new StringContent((variant.Stock ?? 0).ToString()), "Stock" },
                    { new StringContent((variant.SalePrice ?? 0).ToString()), "SalePrice" },
                    { new StringContent((variant.OriginalPrice ?? 0).ToString()), "OriginalPrice" },
                    { new StringContent(variant.Status ?? string.Empty), "Status" }
                };

                if (ImageFile is { Length: > 0 })
                {
                    var img = new StreamContent(ImageFile.OpenReadStream());
                    img.Headers.ContentLength = ImageFile.Length;
                    img.Headers.ContentType = new MediaTypeHeaderValue(ImageFile.ContentType);
                    formData.Add(img, "ImageFile", ImageFile.FileName);
                }

                foreach (var f in (GalleryFiles ?? new()))
                {
                    if (f?.Length > 0)
                    {
                        var sc = new StreamContent(f.OpenReadStream());
                        sc.Headers.ContentLength = f.Length;
                        sc.Headers.ContentType = new MediaTypeHeaderValue(f.ContentType);
                        formData.Add(sc, "GalleryFiles", f.FileName);
                    }
                }

                foreach (var n in (RemoveGallery ?? Array.Empty<string>()))
                    formData.Add(new StringContent(n), "RemoveGallery");

                var resp = await _client.PutAsync($"api/ProductVariants/{id}", formData);
                if (resp.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Cập nhật biến thể thành công!";
                    return RedirectToAction("Details", "SanPham", new { id = variant.ProductId });
                }

                ModelState.AddModelError("", "Lỗi khi cập nhật: " + await resp.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update variant error");
                ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật biến thể.");
            }

            await LoadProductColorSize(variant.ProductId);
            return View(variant);
        }
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _client.GetAsync($"api/ProductVariants/{id}");
            if (!resp.IsSuccessStatusCode) return NotFound();

            var model = await resp.Content.ReadFromJsonAsync<ProductVariant>();
            return View(model); // View bạn đã dán
        }

        // ============== DELETE (optional) ==============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int productId)
        {
            var resp = await _client.DeleteAsync($"api/ProductVariants/{id}");
            TempData[(resp.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage")] =
                resp.IsSuccessStatusCode ? "Xoá biến thể thành công!" : "Không thể xoá biến thể.";
            return RedirectToAction("Details", "SanPham", new { id = productId });
        }


        // ============== Helpers ==============
        private async Task LoadProductColorSize(int? selectedProductId = null)
        {
            var products = await _client.GetFromJsonAsync<List<Product>>("api/Product") ?? new();
            var colors = await _client.GetFromJsonAsync<List<DATN1WEB.Models.Color>>("api/Colors") ?? new();
            var sizes = await _client.GetFromJsonAsync<List<DATN1WEB.Models.Size>>("api/Sizes") ?? new();

            ViewBag.Products = new SelectList(products, "ProductId", "ProductName", selectedProductId);
            ViewBag.Colors = new SelectList(colors, "ColorId", "ColorName");
            ViewBag.Sizes = new SelectList(sizes, "SizeId", "SizeName");
        }
    }
}
