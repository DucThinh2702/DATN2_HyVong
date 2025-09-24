   
    using DATN1API.Models;
    using DATN1WEB.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.Extensions.Logging;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Linq;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
using System.Net;
using DATN1API.Services;
using Microsoft.AspNetCore.Identity;

public class SanPhamController : Controller
{
    private readonly HttpClient _client;
    private readonly ILogger<SanPhamController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PermissionService _permissionService;

    public SanPhamController(
        IHttpClientFactory httpClientFactory,
        ILogger<SanPhamController> logger,
        UserManager<ApplicationUser> userManager,
        PermissionService permissionService)
    {
        _client = httpClientFactory.CreateClient("api");
        _logger = logger;
        _userManager = userManager;
        _permissionService = permissionService;
    }

    private static int TotalStock(Product? p)
        => p?.ProductVariants?.Sum(v => v.Stock ?? 0) ?? 0;


    // ======================= INDEX =======================
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        // ✅ kiểm tra quyền XEM
        var user = await _userManager.GetUserAsync(User);
        if (!await _permissionService.HasPermission(user, "SanPham", "View"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xem sản phẩm!";
            return RedirectToAction("Index", "Home");
        }
        const int pageSize = 6;

            try
            {
                ViewBag.ApiUrl = _client.BaseAddress?.ToString() ?? "https://localhost:5002/";

                var products = await _client.GetFromJsonAsync<List<Product>>("api/Product")
                               ?? new List<Product>();

                // Sắp xếp mới nhất lên đầu
                products = products
                    .OrderByDescending(p => p.CreatedDate ?? DateTime.MinValue)
                    .ThenByDescending(p => p.ProductId)
                    .ToList();

                // Tìm kiếm (nếu có)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var key = search.Trim().ToLower();
                    products = products
                        .Where(p => (p.ProductName ?? string.Empty).ToLower().Contains(key))
                        .ToList();
                }

                // Thống kê
                ViewBag.TotalCount = products.Count;
                ViewBag.InStockCount = products.Count(p => TotalStock(p) > 0);
                ViewBag.OutOfStockCount = products.Count(p => TotalStock(p) == 0);

                var lowStock = products.Where(p => {
                    var t = TotalStock(p);
                    return t > 0 && t <= 5;
                }).ToList();
                ViewBag.LowStockProducts = lowStock;
                ViewBag.LowStockCount = lowStock.Count;

                // Phân trang
                var totalItems = products.Count;
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                if (page < 1) page = 1;
                if (page > totalPages && totalPages > 0) page = totalPages;

                var pageData = products
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = totalPages;
                ViewBag.Search = search ?? string.Empty;

                return View(pageData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách sản phẩm");
                ViewBag.TotalCount = 0;
                ViewBag.InStockCount = 0;
                ViewBag.OutOfStockCount = 0;
                ViewBag.LowStockProducts = new List<Product>();
                ViewBag.LowStockCount = 0;
                ViewBag.Page = 1;
                ViewBag.PageSize = 6;
                ViewBag.TotalPages = 0;
                ViewBag.Search = search ?? string.Empty;
                return View(new List<Product>());
            }
        }

    // ======================= CREATE (GET) =======================
    // ======================= CREATE (GET) =======================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
        if (!await _permissionService.HasPermission(user, "SanPham", "Create"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền thêm sản phẩm!";
            return RedirectToAction(nameof(Index));
        }

        await LoadCategoryColorSize();
        return View();
    }
    // ======================= CREATE (POST) =======================
    [HttpPost]
    public async Task<IActionResult> Create(Product product)
    {
        var user = await _userManager.GetUserAsync(User);
        if (!await _permissionService.HasPermission(user, "SanPham", "Create"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền thêm sản phẩm!";
            return RedirectToAction(nameof(Index));
        }
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

                formData.Add(new StringContent(product.ProductName ?? ""), "ProductName");
                formData.Add(new StringContent(product.Description ?? ""), "Description");
                formData.Add(new StringContent(product.OriginalPrice?.ToString() ?? "0"), "OriginalPrice");
                formData.Add(new StringContent(product.SalePrice?.ToString() ?? "0"), "SalePrice");
                formData.Add(new StringContent(product.Material ?? ""), "Material");
                formData.Add(new StringContent(product.CategoryId?.ToString() ?? ""), "CategoryId");
                formData.Add(new StringContent(string.IsNullOrWhiteSpace(product.Status) ? "Đang bán" : product.Status!), "Status");

                if (product.ImageFile != null)
                {
                    var imageContent = new StreamContent(product.ImageFile.OpenReadStream());
                    imageContent.Headers.ContentLength = product.ImageFile.Length;
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue(product.ImageFile.ContentType);
                    formData.Add(imageContent, "ImageFile", product.ImageFile.FileName);
                }

                // Biến thể
                var variants = product.ProductVariants.ToList();
                for (int i = 0; i < variants.Count; i++)
                {
                    var v = variants[i];
                    formData.Add(new StringContent(v.ColorId.ToString()), $"ProductVariants[{i}].ColorId");
                    formData.Add(new StringContent(v.SizeId.ToString()), $"ProductVariants[{i}].SizeId");
                    formData.Add(new StringContent((v.Stock ?? 0).ToString()), $"ProductVariants[{i}].Stock");
                    formData.Add(new StringContent((v.SalePrice ?? 0).ToString()), $"ProductVariants[{i}].SalePrice");
                    formData.Add(new StringContent((v.OriginalPrice ?? 0).ToString()), $"ProductVariants[{i}].OriginalPrice");
                    formData.Add(new StringContent(string.IsNullOrWhiteSpace(v.Status) ? "Active" : v.Status!), $"ProductVariants[{i}].Status");

                    if (v.ImageFile != null)
                    {
                        var vImage = new StreamContent(v.ImageFile.OpenReadStream());
                        vImage.Headers.ContentLength = v.ImageFile.Length;
                        vImage.Headers.ContentType = new MediaTypeHeaderValue(v.ImageFile.ContentType);
                        formData.Add(vImage, $"ProductVariants[{i}].ImageFile", v.ImageFile.FileName);
                    }
                }

                var response = await _client.PostAsync("api/Product/CreateFull", formData);

                if (response.IsSuccessStatusCode)
                {
                    // Cố gắng lấy id sản phẩm mới để quay về trang chi tiết
                    Product? created = null;
                    try { created = await response.Content.ReadFromJsonAsync<Product>(); } catch { /* ignore */ }

                    TempData["SuccessMessage"] = "Thêm sản phẩm thành công!";

                    if (created?.ProductId > 0)
                    {
                        // Hiển thị toast CHỈ khi có flash
                        return RedirectToAction("Details", new { id = created.ProductId, flash = "product-created" });
                    }

                    // Fallback về Index (vẫn có flash)
                    return RedirectToAction(nameof(Index), new { flash = "product-created" });
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

    // ======================= EDIT (GET) =======================
    // ======================= EDIT (GET) =======================
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (!await _permissionService.HasPermission(user, "SanPham", "Edit"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền sửa sản phẩm!";
            return RedirectToAction(nameof(Index));
        }
        try
            {
                var product = await _client.GetFromJsonAsync<Product>($"api/Product/{id}");
                if (product == null) return NotFound();

                await LoadCategoryColorSize();
                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy sản phẩm để chỉnh sửa (id={Id})", id);
                return StatusCode(500, "Lỗi server");
            }
        }

    // ======================= EDIT (POST) =======================
    // ======================= EDIT (POST) =======================
    [HttpPost]
    public async Task<IActionResult> Edit(int id, Product product, IFormFile? ImageFile)
    {
        var user = await _userManager.GetUserAsync(User);
        if (!await _permissionService.HasPermission(user, "SanPham", "Edit"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền sửa sản phẩm!";
            return RedirectToAction(nameof(Index));
        }
        if (id != product.ProductId) return BadRequest();

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
                formData.Add(new StringContent((product.SalePrice ?? 0).ToString()), "SalePrice");
                formData.Add(new StringContent((product.OriginalPrice ?? 0).ToString()), "OriginalPrice");
                formData.Add(new StringContent(product.Material ?? ""), "Material");
                formData.Add(new StringContent(product.CategoryId?.ToString() ?? ""), "CategoryId");
                formData.Add(new StringContent(product.Status ?? ""), "Status");

                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var imageContent = new StreamContent(ImageFile.OpenReadStream());
                    imageContent.Headers.ContentLength = ImageFile.Length;
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue(ImageFile.ContentType);
                    formData.Add(imageContent, "ImageFile", ImageFile.FileName);
                }

                var response = await _client.PutAsync($"api/Product/{id}", formData);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Cập nhật sản phẩm thành công!";
                    // Quay về trang chi tiết có flash để chỉ hiển thị toast khi vừa cập nhật
                    return RedirectToAction("Details", new { id = id, flash = "product-updated" });
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

    // ======================= DELETE (GET) =======================
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (!await _permissionService.HasPermission(user, "SanPham", "Delete"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xoá sản phẩm!";
            return RedirectToAction(nameof(Index));
        }
        try
            {
                var product = await _client.GetFromJsonAsync<Product>($"api/Product/{id}");
                if (product == null) return NotFound();

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy sản phẩm để xoá (id={Id})", id);
                return StatusCode(500, "Lỗi server");
            }
        }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (!await _permissionService.HasPermission(user, "SanPham", "Delete"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xoá sản phẩm!";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var resp = await _client.DeleteAsync($"api/Product/{id}");

            if (resp.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Xoá sản phẩm thành công!";
                return RedirectToAction(nameof(Index), new { flash = "product-deleted" });
            }

            var raw = await resp.Content.ReadAsStringAsync();
            if (IsFkDeleteConflict(resp.StatusCode, raw))
            {
                TempData["ErrorMessage"] =
                    "Không thể xoá sản phẩm vì đã có khách đặt mua hàng";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể xoá sản phẩm. Vui lòng thử lại sau.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xoá sản phẩm id={Id}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi xoá sản phẩm. Vui lòng thử lại sau.";
        }

        // Lỗi thì quay về Index (không gắn flash để tránh show toast nhầm)
        return RedirectToAction(nameof(Index));
    }

    private static bool IsFkDeleteConflict(HttpStatusCode statusCode, string? body)
    {
        if (statusCode == HttpStatusCode.Conflict) return true; // API đã xử lý và trả 409
        var s = (body ?? string.Empty).ToLowerInvariant();
        // fallback: API chưa chuẩn hoá, dò chuỗi từ SQL Server
        return s.Contains("conflicted with the reference constraint") ||
               s.Contains("delete statement conflicted") ||
               s.Contains("foreign key constraint") ||
               s.Contains("fk_orderdetails") ||
               s.Contains("productvariantid");
    }

    // ===== Load Data cho Create/Edit =====
    private async Task LoadCategoryColorSize()
    {
        var categories = await _client.GetFromJsonAsync<List<Category>>("api/Categories") ?? new();
        var colors = await _client.GetFromJsonAsync<List<DATN1WEB.Models.Color>>("api/Colors") ?? new();
        var sizes = await _client.GetFromJsonAsync<List<DATN1WEB.Models.Size>>("api/Sizes") ?? new();

        ViewBag.Categories = new SelectList(categories, "CategoryId", "CategoryName");
        ViewBag.Colors = colors;
        ViewBag.Sizes = sizes;
    }
}

