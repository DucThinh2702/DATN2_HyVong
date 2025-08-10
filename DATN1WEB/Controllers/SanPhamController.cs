using DATN1API.Models;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

public class SanPhamController : Controller
{
    private readonly HttpClient _client;
    private readonly ILogger<SanPhamController> _logger;

    public SanPhamController(IHttpClientFactory httpClientFactory, ILogger<SanPhamController> logger)
    {
        _client = httpClientFactory.CreateClient("api");
        _logger = logger;
    }

    // ======================= INDEX =======================
    public async Task<IActionResult> Index()
    {
        try
        {
            // Gán URL API cho View
            ViewBag.ApiUrl = _client.BaseAddress?.ToString() ?? "https://localhost:5002/";

            var products = await _client.GetFromJsonAsync<List<Product>>("api/Product") ?? new List<Product>();

            _logger.LogInformation("Số sản phẩm từ API: {Count}", products.Count);

            ViewBag.TotalCount = products.Count;
            ViewBag.InStockCount = products.Count(x => x.ProductVariants != null && x.ProductVariants.Any(v => v.Stock > 0));
            ViewBag.OutOfStockCount = products.Count(x => x.ProductVariants != null && x.ProductVariants.All(v => v.Stock == 0));
            ViewBag.LowStockProducts = products
                .Where(x => x.ProductVariants != null && x.ProductVariants.Any(v => v.Stock > 0 && v.Stock <= 5))
                .ToList();
            ViewBag.LowStockCount = ViewBag.LowStockProducts.Count;

            return View(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách sản phẩm");
            return View(new List<Product>());
        }
    }

    // ======================= CREATE (GET) =======================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadCategoryColorSize();
        return View();
    }

    // ======================= CREATE (POST) =======================
    [HttpPost]
    public async Task<IActionResult> Create(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.ProductName))
        {
            ModelState.AddModelError("", "Tên sản phẩm là bắt buộc");
            await LoadCategoryColorSize();
            return View(product);
        }

        if (product.ProductVariants == null || !product.ProductVariants.Any())
        {
            ModelState.AddModelError("", "Cần ít nhất một biến thể (Color & Size).");
            await LoadCategoryColorSize();
            return View(product);
        }

        try
        {
            var formData = new MultipartFormDataContent();

            // ===== Thông tin sản phẩm =====
            formData.Add(new StringContent(product.ProductName ?? ""), "ProductName");
            formData.Add(new StringContent(product.Description ?? ""), "Description");
            formData.Add(new StringContent(product.OriginalPrice?.ToString() ?? "0"), "OriginalPrice");
            formData.Add(new StringContent(product.SalePrice?.ToString() ?? "0"), "SalePrice");
            formData.Add(new StringContent(product.Material ?? ""), "Material");
            formData.Add(new StringContent(product.CategoryId?.ToString() ?? ""), "CategoryId");
            formData.Add(new StringContent(product.Status ?? "Đang bán"), "Status");

            // ===== Ảnh chính =====
            if (product.ImageFile != null)
            {
                var imageContent = new StreamContent(product.ImageFile.OpenReadStream())
                {
                    Headers =
                    {
                        ContentLength = product.ImageFile.Length,
                        ContentType = new MediaTypeHeaderValue(product.ImageFile.ContentType)
                    }
                };
                formData.Add(imageContent, "ImageFile", product.ImageFile.FileName);
            }

            // ===== Biến thể =====
            var variants = product.ProductVariants.ToList();
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                formData.Add(new StringContent(variant.ColorId.ToString()), $"ProductVariants[{i}].ColorId");
                formData.Add(new StringContent(variant.SizeId.ToString()), $"ProductVariants[{i}].SizeId");
                formData.Add(new StringContent(variant.Stock?.ToString() ?? "0"), $"ProductVariants[{i}].Stock");
                formData.Add(new StringContent(variant.SalePrice?.ToString() ?? "0"), $"ProductVariants[{i}].SalePrice");
                formData.Add(new StringContent(variant.OriginalPrice?.ToString() ?? "0"), $"ProductVariants[{i}].OriginalPrice");
                formData.Add(new StringContent(variant.Status ?? "Active"), $"ProductVariants[{i}].Status");

                if (variant.ImageFile != null)
                {
                    var vImageContent = new StreamContent(variant.ImageFile.OpenReadStream())
                    {
                        Headers =
                        {
                            ContentLength = variant.ImageFile.Length,
                            ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(variant.ImageFile.ContentType)
                        }
                    };
                    formData.Add(vImageContent, $"ProductVariants[{i}].ImageFile", variant.ImageFile.FileName);
                }
            }

            var response = await _client.PostAsync("api/Product/CreateFull", formData);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Thêm sản phẩm và biến thể thành công!";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Lỗi khi thêm sản phẩm: " + await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thêm sản phẩm");
            ModelState.AddModelError("", "Có lỗi xảy ra khi thêm sản phẩm.");
        }

        await LoadCategoryColorSize();
        return View(product);
    }

    // ======================= DETAILS =======================
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var product = await _client.GetFromJsonAsync<Product>($"api/Product/{id}");
            if (product == null) return NotFound();
            return View(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy chi tiết sản phẩm id={Id}", id);
            return StatusCode(500, "Lỗi server");
        }
    }

    // ======================= DELETE =======================
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var response = await _client.DeleteAsync($"api/Product/{id}");
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Xoá sản phẩm thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể xoá sản phẩm: " + await response.Content.ReadAsStringAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xoá sản phẩm id={Id}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi xoá sản phẩm.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ======================= DELETE (GET): Hiển thị form xác nhận =======================
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var product = await _client.GetFromJsonAsync<Product>($"api/Product/{id}");
            if (product == null) return NotFound();

            return View(product); // Trả về view Delete.cshtml
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy sản phẩm để xoá (id={Id})", id);
            return StatusCode(500, "Lỗi server");
        }
    }

    // ======================= Load Data =======================
    private async Task LoadCategoryColorSize()
    {
        var categories = await _client.GetFromJsonAsync<List<Category>>("api/Categories") ?? new();
        var colors = await _client.GetFromJsonAsync<List<DATN1WEB.Models.Color>>("api/Colors") ?? new();
        var sizes = await _client.GetFromJsonAsync<List<DATN1WEB.Models.Size>>("api/Sizes") ?? new();

        ViewBag.Categories = new SelectList(categories, "CategoryId", "CategoryName");
        ViewBag.Colors = colors;
        ViewBag.Sizes = sizes;
    }
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var product = await _client.GetFromJsonAsync<Product>($"api/Product/{id}");
            if (product == null) return NotFound();

            await LoadCategoryColorSize(); // load danh mục
            return View(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy sản phẩm để chỉnh sửa (id={Id})", id);
            return StatusCode(500, "Lỗi server");
        }
    }
    [HttpPost]
    public async Task<IActionResult> Edit(int id, Product product, IFormFile? ImageFile)
    {
        if (id != product.ProductId)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            await LoadCategoryColorSize();
            return View(product);
        }

        try
        {
            var formData = new MultipartFormDataContent();

            formData.Add(new StringContent(product.ProductId.ToString()), "ProductId");
            formData.Add(new StringContent(product.ProductName ?? ""), "ProductName");
            formData.Add(new StringContent(product.Description ?? ""), "Description");
            formData.Add(new StringContent(product.SalePrice?.ToString() ?? "0"), "SalePrice");
            formData.Add(new StringContent(product.OriginalPrice?.ToString() ?? "0"), "OriginalPrice");
            formData.Add(new StringContent(product.Material ?? ""), "Material");
            formData.Add(new StringContent(product.CategoryId?.ToString() ?? ""), "CategoryId");
            formData.Add(new StringContent(product.Status ?? ""), "Status");

            // Gửi ảnh mới nếu có
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

            var response = await _client.PutAsync($"api/Product/{id}", formData);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Cập nhật sản phẩm thành công!";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", "Lỗi khi cập nhật: " + await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật sản phẩm");
            ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật sản phẩm.");
        }

        await LoadCategoryColorSize();
        return View(product);
    }

}
