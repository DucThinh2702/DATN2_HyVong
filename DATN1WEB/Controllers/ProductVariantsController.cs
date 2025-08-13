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

        // ======================= INDEX =======================
        // Optional filter by productId
        public async Task<IActionResult> Index(int? productId)
        {
            try
            {
                // chuẩn hoá base url để ghép ảnh
                ViewBag.ApiUrl = (_client.BaseAddress?.ToString() ?? "https://localhost:5002/").TrimEnd('/');

                var url = "api/ProductVariants" + (productId.HasValue ? $"/by-product/{productId.Value}" : "");
                var variants = await _client.GetFromJsonAsync<List<ProductVariant>>(url) ?? new List<ProductVariant>();

                ViewBag.ProductId = productId;
                ViewBag.TotalCount = variants.Count;
                ViewBag.InStockCount = variants.Count(v => (v.Stock ?? 0) > 0);
                ViewBag.OutOfStockCount = variants.Count(v => (v.Stock ?? 0) == 0);

                return View(variants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách biến thể");
                return View(new List<ProductVariant>());
            }
        }


        // ======================= DETAILS =======================
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var variant = await _client.GetFromJsonAsync<ProductVariant>($"api/ProductVariants/{id}");
                if (variant == null) return NotFound();
                return View(variant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy chi tiết biến thể id={Id}", id);
                return StatusCode(500, "Lỗi server");
            }
        }

        // ======================= CREATE (GET) =======================
        [HttpGet]
        public async Task<IActionResult> Create(int? productId)
        {
            await LoadProductColorSize();
            ViewBag.PreselectProductId = productId;
            return View(new ProductVariant { ProductId = productId ?? 0, Status = "Active" });
        }

        // ======================= CREATE (POST) =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductVariant variant)
        {
            if (variant.ProductId <= 0)
                ModelState.AddModelError("ProductId", "Vui lòng chọn sản phẩm");
            if (variant.ColorId <= 0)
                ModelState.AddModelError("ColorId", "Vui lòng chọn màu sắc");
            if (variant.SizeId <= 0)
                ModelState.AddModelError("SizeId", "Vui lòng chọn kích cỡ");

            if (!ModelState.IsValid)
            {
                await LoadProductColorSize();
                return View(variant);
            }

            try
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(variant.ProductId.ToString()), "ProductId");
                formData.Add(new StringContent(variant.ColorId.ToString()), "ColorId");
                formData.Add(new StringContent(variant.SizeId.ToString()), "SizeId");
                formData.Add(new StringContent(variant.Sku ?? string.Empty), "Sku");
                formData.Add(new StringContent((variant.Stock ?? 0).ToString(CultureInfo.InvariantCulture)), "Stock");
                formData.Add(new StringContent((variant.SalePrice ?? 0).ToString(CultureInfo.InvariantCulture)), "SalePrice");
                formData.Add(new StringContent((variant.OriginalPrice ?? 0).ToString(CultureInfo.InvariantCulture)), "OriginalPrice");
                formData.Add(new StringContent(variant.Status ?? "Active"), "Status");

                if (variant.ImageFile != null && variant.ImageFile.Length > 0)
                {
                    var imageContent = new StreamContent(variant.ImageFile.OpenReadStream());
                    imageContent.Headers.ContentLength = variant.ImageFile.Length;
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue(variant.ImageFile.ContentType);
                    formData.Add(imageContent, "ImageFile", variant.ImageFile.FileName);
                }

                var response = await _client.PostAsync("api/ProductVariants", formData);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Thêm biến thể thành công!";
                    return RedirectToAction(nameof(Index), new { productId = variant.ProductId });
                }

                var err = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, "Lỗi khi thêm biến thể: " + err);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm biến thể");
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi thêm biến thể.");
            }

            await LoadProductColorSize();
            return View(variant);
        }

        // ======================= EDIT (GET) =======================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var variant = await _client.GetFromJsonAsync<ProductVariant>($"api/ProductVariants/{id}");
                if (variant == null) return NotFound();
                await LoadProductColorSize();
                return View(variant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy biến thể để chỉnh sửa (id={Id})", id);
                return StatusCode(500, "Lỗi server");
            }
        }

        // ======================= EDIT (POST) =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductVariant variant, IFormFile? ImageFile)
        {
            if (id != variant.VariantId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await LoadProductColorSize();
                return View(variant);
            }

            try
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(variant.VariantId.ToString()), "VariantId");
                formData.Add(new StringContent(variant.ProductId.ToString()), "ProductId");
                formData.Add(new StringContent(variant.ColorId.ToString()), "ColorId");
                formData.Add(new StringContent(variant.SizeId.ToString()), "SizeId");
                formData.Add(new StringContent(variant.Sku ?? string.Empty), "Sku");
                formData.Add(new StringContent((variant.Stock ?? 0).ToString()), "Stock");
                formData.Add(new StringContent((variant.SalePrice ?? 0).ToString()), "SalePrice");
                formData.Add(new StringContent((variant.OriginalPrice ?? 0).ToString()), "OriginalPrice");
                formData.Add(new StringContent(variant.Status ?? string.Empty), "Status");

                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var imageContent = new StreamContent(ImageFile.OpenReadStream())
                    {
                        Headers =
                        {
                            ContentLength = ImageFile.Length,
                            ContentType = new MediaTypeHeaderValue(ImageFile.ContentType)
                        }
                    };
                    formData.Add(imageContent, "ImageFile", ImageFile.FileName);
                }

                var response = await _client.PutAsync($"api/ProductVariants/{id}", formData);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Cập nhật biến thể thành công!";
                    return RedirectToAction(nameof(Index), new { productId = variant.ProductId });
                }

                ModelState.AddModelError("", "Lỗi khi cập nhật: " + await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật biến thể");
                ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật biến thể.");
            }

            await LoadProductColorSize();
            return View(variant);
        }

        // ======================= DELETE (GET) =======================
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var variant = await _client.GetFromJsonAsync<ProductVariant>($"api/ProductVariants/{id}");
                if (variant == null) return NotFound();
                return View(variant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy biến thể để xoá (id={Id})", id);
                return StatusCode(500, "Lỗi server");
            }
        }

        // ======================= DELETE (POST) =======================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, int productId)
        {
            try
            {
                var response = await _client.DeleteAsync($"api/ProductVariants/{id}");
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Xoá biến thể thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không thể xoá biến thể: " + await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xoá biến thể id={Id}", id);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xoá biến thể.";
            }
            return RedirectToAction(nameof(Index), new { productId });
        }

        // ======================= Helpers =======================
        private async Task LoadProductColorSize()
        {
            var products = await _client.GetFromJsonAsync<List<Product>>("api/Product") ?? new();
            var colors = await _client.GetFromJsonAsync<List<DATN1WEB.Models.Color>>("api/Colors") ?? new();
            var sizes = await _client.GetFromJsonAsync<List<DATN1WEB.Models.Size>>("api/Sizes") ?? new();

            ViewBag.Products = new SelectList(products, "ProductId", "ProductName");
            ViewBag.Colors = new SelectList(colors, "ColorId", "ColorName");
            ViewBag.Sizes = new SelectList(sizes, "SizeId", "SizeName");
        }
    }
}
