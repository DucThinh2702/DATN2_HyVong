//using System;
//using System.Linq;
//using System.Globalization;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using DATN1API.Data;
//using DATN1API.Models;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//public class OrdersController : Controller
//{
//    private readonly DatnContext _ctx;
//    public OrdersController(DatnContext ctx) => _ctx = ctx;

//    // ===== Payment helpers =====
//    private static string NormalizePayment(string? s)
//    {
//        var x = (s ?? "").Trim().ToLowerInvariant();
//        return x switch
//        {
//            "paid" or "đã thanh toán" or "da thanh toan" => "Paid",
//            "unpaid" or "chưa thanh toán" or "chua thanh toan" => "Unpaid",
//            "pending" or "chờ thanh toán" or "cho thanh toan" => "Pending",
//            _ => "Unknown"
//        };
//    }
//    private static string[] PaymentAliases(string? s)
//    {
//        switch (NormalizePayment(s))
//        {
//            case "Paid":
//                return new[] { "Paid", "Đã thanh toán", "da thanh toan", "Đã thanh toán" };
//            case "Unpaid":
//                return new[] { "Unpaid", "Chưa thanh toán", "chua thanh toan" };
//            case "Pending":
//                return new[] { "Pending", "Chờ thanh toán", "cho thanh toan" };
//            default:
//                return Array.Empty<string>();
//        }
//    }

//    // ===== OrderStatus helpers =====
//    private static string NormalizeStatus(string? s)
//    {
//        var x = (s ?? "").Trim().ToLowerInvariant();
//        return x switch
//        {
//            "" => "Pending",
//            "pending" or "chờ xác nhận" => "Pending",
//            "processing" or "đang chuẩn bị" or "đang xử lý" or "chờ xử lý" => "Processing",
//            "shipping" or "đang giao" or "vận chuyển" => "Shipping",
//            "completed" or "delivered" or "hoàn tất" or "hoàn thành" => "Completed",
//            "cancelled" or "canceled" or "đã huỷ" or "đã hủy" => "Cancelled",
//            _ => "Unknown"
//        };
//    }

//    // ================== LIST + SEARCH + FILTER + PAGINATION ==================
//    // pageSize cố định = 10 theo yêu cầu
//    public async Task<IActionResult> Index(
//        string? search,
//        string? orderStatus,
//        string? paymentStatus,
//        int page = 1)
//    {
//        const int pageSize = 10;

//        var q = _ctx.Orders
//            .Include(o => o.OrderDetails)
//            .AsQueryable();

//        // ---- Search
//        if (!string.IsNullOrWhiteSpace(search))
//        {
//            search = search.Trim();
//            q = q.Where(o =>
//                EF.Functions.Like(o.OrderId.ToString(), $"%{search}%") ||
//                EF.Functions.Like(o.RecipientName ?? "", $"%{search}%") ||
//                EF.Functions.Like(o.RecipientPhone ?? "", $"%{search}%") ||
//                EF.Functions.Like(o.DeliveryAddress ?? "", $"%{search}%") ||
//                EF.Functions.Like(o.Note ?? "", $"%{search}%")
//            );
//        }

//        // ---- Filter OrderStatus (chấp nhận EN & VI)
//        if (!string.IsNullOrWhiteSpace(orderStatus))
//        {
//            var norm = NormalizeStatus(orderStatus);
//            if (norm != "Unknown")
//            {
//                q = q.Where(o => NormalizeStatus(o.OrderStatus) == norm);
//            }
//            else
//            {
//                // nếu chọn status không hợp lệ => không ra kết quả
//                q = q.Where(o => false);
//            }
//        }

//        // ---- Filter PaymentStatus (EN & VI)
//        if (!string.IsNullOrWhiteSpace(paymentStatus))
//        {
//            var aliases = PaymentAliases(paymentStatus);
//            if (aliases.Length > 0)
//                q = q.Where(o => aliases.Contains(o.PaymentStatus!));
//            else
//                q = q.Where(o => false);
//        }

//        // ---- Tổng theo bộ lọc hiện tại (để thống kê đúng ngữ cảnh)
//        var filteredTotal = await q.CountAsync();

//        // ---- Sắp xếp: mới nhất lên đầu
//        q = q.OrderByDescending(o => o.OrderDate ?? DateTime.MinValue);

//        // ---- Phân trang (10/trang)
//        var totalPages = (int)Math.Ceiling(filteredTotal / (double)pageSize);
//        page = Math.Max(1, (totalPages == 0 ? 1 : Math.Min(page, totalPages)));

//        var items = await q
//            .Skip((page - 1) * pageSize)
//            .Take(pageSize)
//            .ToListAsync();

//        // ---- Thống kê phụ (trên toàn bộ Orders, bạn có thể đổi sang filtered nếu muốn)
//        ViewBag.TotalOrders = await _ctx.Orders.CountAsync();
//        ViewBag.PendingCount = await _ctx.Orders.CountAsync(o => NormalizeStatus(o.OrderStatus) == "Pending");
//        ViewBag.PaidCount = await _ctx.Orders.CountAsync(o => PaymentAliases("Paid").Contains(o.PaymentStatus!));
//        ViewBag.UnpaidCount = await _ctx.Orders.CountAsync(o => PaymentAliases("Unpaid").Contains(o.PaymentStatus!));

//        // ---- Thông tin phân trang
//        ViewBag.Page = page;
//        ViewBag.PageSize = pageSize;
//        ViewBag.TotalPages = totalPages;
//        ViewBag.FilteredTotal = filteredTotal;

//        return View(items);
//    }


//// ================== DETAILS ==================
//public async Task<IActionResult> Details(int id)
//    {
//        var order = await _ctx.Orders
//            .Include(o => o.User)
//            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Product)
//            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Color)
//            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Size)
//            .FirstOrDefaultAsync(o => o.OrderId == id);

//        if (order == null) return NotFound();

//        decimal itemsSubtotal = order.OrderDetails?.Sum(od =>
//            (od.TotalPrice ?? (od.UnitPrice ?? 0m) * (od.Quantity ?? 0))) ?? 0m;

//        ViewBag.ItemsSubtotal = itemsSubtotal;
//        ViewBag.ShippingFee = order.ShippingFee ?? 0m;

//        var baseTotal = order.TotalAmount ?? itemsSubtotal;
//        ViewBag.GrandTotal = baseTotal + (order.ShippingFee ?? 0m);

//        return View(order);
//    }

//    // ================== EDIT ==================
//    public class EditOrderVm
//    {
//        public int OrderId { get; set; }
//        public string? RecipientName { get; set; }
//        public string? RecipientPhone { get; set; }
//        public string? DeliveryAddress { get; set; }
//        public string? Note { get; set; }
//        public int? PromoCode { get; set; }
//        public string? PaymentStatus { get; set; }
//        public string? OrderStatus { get; set; }
//        public string? ShippingFee { get; set; }
//        public List<LineVm> Lines { get; set; } = new();
//    }
//    public class LineVm
//    {
//        public int OrderDetailId { get; set; }
//        public int? Quantity { get; set; }
//        public string? UnitPrice { get; set; }
//    }

//    private static string NormalizeStatus(string? s)
//    {
//        var x = (s ?? "").Trim().ToLowerInvariant();
//        return x switch
//        {
//            "" => "Pending",
//            "pending" or "chờ xác nhận" => "Pending",
//            "processing" or "đang chuẩn bị" or "đang xử lý" or "chờ xử lý" => "Processing",
//            "shipping" or "đang giao" or "vận chuyển" => "Shipping",
//            "completed" or "delivered" or "hoàn tất" or "hoàn thành" => "Completed",
//            "cancelled" or "canceled" or "đã huỷ" or "đã hủy" => "Cancelled",
//            _ => "Unknown"
//        };
//    }
//    private static int StatusRank(string s) => s switch
//    {
//        "Pending" => 0,
//        "Processing" => 1,
//        "Shipping" => 2,
//        "Completed" => 3,
//        _ => -1
//    };
//    private static string ViOrderLabel(string s) => s switch
//    {
//        "Pending" => "Chờ xác nhận",
//        "Processing" => "Đang chuẩn bị",
//        "Shipping" => "Đang giao",
//        "Completed" => "Hoàn tất",
//        "Cancelled" => "Đã huỷ",
//        _ => "Không rõ"
//    };

//    [HttpGet]
//    public async Task<IActionResult> Edit(int id)
//    {
//        var order = await _ctx.Orders
//            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Product)
//            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Color)
//            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Size)
//            .FirstOrDefaultAsync(o => o.OrderId == id);

//        if (order == null) return NotFound();
//        return View(order);
//    }

//    [HttpPost]
//    [ValidateAntiForgeryToken]
//    public async Task<IActionResult> Edit(EditOrderVm vm)
//    {
//        var order = await _ctx.Orders
//            .Include(o => o.OrderDetails)
//            .FirstOrDefaultAsync(o => o.OrderId == vm.OrderId);

//        if (order == null) return NotFound();

//        var errors = new List<string>();

//        var currentStatus = NormalizeStatus(order.OrderStatus);
//        var targetStatus = NormalizeStatus(vm.OrderStatus);
//        var targetPayment = NormalizePayment(vm.PaymentStatus);

//        if (targetStatus == "Unknown") errors.Add("Trạng thái đơn không hợp lệ.");
//        if (targetPayment == "Unknown") errors.Add("Trạng thái thanh toán không hợp lệ.");

//        if (targetStatus != "Cancelled" && StatusRank(targetStatus) < StatusRank(currentStatus))
//            errors.Add($"Không thể cập nhật lùi trạng thái từ “{ViOrderLabel(currentStatus)}” về “{ViOrderLabel(targetStatus)}”.");

//        if (targetStatus == "Cancelled")
//        {
//            if (currentStatus is "Completed" or "Cancelled")
//                errors.Add("Đơn đã hoàn tất hoặc đã huỷ, không thể thay đổi.");
//            else if (currentStatus is not ("Pending" or "Processing"))
//                errors.Add("Chỉ được huỷ khi đơn đang ở trạng thái “Chờ xác nhận” hoặc “Đang chuẩn bị”.");
//        }

//        if (targetStatus == "Completed" && targetPayment != "Paid")
//            errors.Add("Chỉ được chuyển sang “Hoàn tất” khi trạng thái thanh toán là “Đã thanh toán”.");

//        decimal shippingFee = 0m;
//        if (!string.IsNullOrWhiteSpace(vm.ShippingFee) &&
//            !decimal.TryParse(vm.ShippingFee, NumberStyles.Any, CultureInfo.InvariantCulture, out shippingFee))
//        {
//            errors.Add("Phí vận chuyển không hợp lệ.");
//        }

//        decimal itemsSubtotal = 0m;
//        for (int i = 0; i < vm.Lines.Count; i++)
//        {
//            var l = vm.Lines[i];
//            var line = order.OrderDetails.FirstOrDefault(d => d.OrderDetailId == l.OrderDetailId);
//            if (line == null) continue;

//            line.Quantity = l.Quantity ?? 0;

//            decimal unit = 0m;
//            if (!string.IsNullOrWhiteSpace(l.UnitPrice) &&
//                !decimal.TryParse(l.UnitPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out unit))
//            {
//                errors.Add($"Dòng #{i + 1}: Đơn giá không hợp lệ.");
//            }
//            line.UnitPrice = unit;
//            line.TotalPrice = unit * (line.Quantity ?? 0);
//            itemsSubtotal += line.TotalPrice ?? 0m;
//        }

//        if (errors.Any())
//        {
//            TempData["ErrorMessage"] = string.Join("<br/>", errors);
//            return RedirectToAction(nameof(Edit), new { id = vm.OrderId });
//        }

//        order.RecipientName = vm.RecipientName;
//        order.RecipientPhone = vm.RecipientPhone;
//        order.DeliveryAddress = vm.DeliveryAddress;
//        order.Note = vm.Note;
//        order.PromoCode = vm.PromoCode;

//        order.OrderStatus = targetStatus;   // Pending/Processing/Shipping/Completed/Cancelled
//        order.PaymentStatus = targetPayment;  // Paid/Unpaid/Pending

//        order.ShippingFee = shippingFee;
//        order.TotalAmount = itemsSubtotal;
//        order.Quantity = order.OrderDetails.Sum(d => d.Quantity ?? 0);

//        await _ctx.SaveChangesAsync();

//        TempData["SuccessMessage"] = "Cập nhật đơn hàng thành công!";
//        return RedirectToAction(nameof(Details), new { id = order.OrderId });
//    }

//    // Heartbeat giữ nguyên (không dùng UpdatedAt)
//    [HttpGet]
//    public IActionResult Heartbeat()
//    {
//        var last = _ctx.Orders
//            .AsNoTracking()
//            .OrderByDescending(o => o.OrderDate ?? DateTime.MinValue)
//            .Select(o => new { o.OrderId, o.OrderStatus, o.PaymentStatus, o.OrderDate })
//            .FirstOrDefault();

//        if (last == null) return Json(new { ok = true, fp = "none" });

//        var stamp = last.OrderDate ?? DateTime.MinValue;
//        var fp = $"{last.OrderId}|{last.OrderStatus}|{last.PaymentStatus}|{stamp:O}";
//        return Json(new { ok = true, fp });
//    }
//}
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class OrdersController : Controller
{
    private readonly DatnContext _ctx;
    public OrdersController(DatnContext ctx) => _ctx = ctx;

    // ===== Helpers: alias ở dạng lowercase để so khớp DB-side =====
    private static string NormalizePayment(string? s)
    {
        var x = (s ?? "").Trim().ToLowerInvariant();
        return x switch
        {
            "paid" or "đã thanh toán" or "da thanh toan" => "Paid",
            "unpaid" or "chưa thanh toán" or "chua thanh toan" => "Unpaid",
            "pending" or "chờ thanh toán" or "cho thanh toan" => "Pending",
            _ => "Unknown"
        };
    }
    private static string[] PaymentAliasesLower(string? s)
    {
        switch (NormalizePayment(s))
        {
            case "Paid":
                return new[] { "paid", "đã thanh toán", "da thanh toan", "đã thanh toán" };
            case "Unpaid":
                return new[] { "unpaid", "chưa thanh toán", "chua thanh toan" };
            case "Pending":
                return new[] { "pending", "chờ thanh toán", "cho thanh toan" };
            default:
                return Array.Empty<string>();
        }
    }

    private static string NormalizeStatus(string? s)
    {
        var x = (s ?? "").Trim().ToLowerInvariant();
        return x switch
        {
            "" => "Pending",
            "pending" or "chờ xác nhận" => "Pending",
            "processing" or "đang chuẩn bị" or "đang xử lý" or "chờ xử lý" => "Processing",
            "shipping" or "đang giao" or "vận chuyển" => "Shipping",
            "completed" or "delivered" or "hoàn tất" or "hoàn thành" => "Completed",
            "cancelled" or "canceled" or "đã huỷ" or "đã hủy" => "Cancelled",
            _ => "Unknown"
        };
    }
    private static string[] OrderStatusAliasesLower(string? s)
    {
        switch (NormalizeStatus(s))
        {
            case "Pending":
                return new[] { "pending", "chờ xác nhận" };
            case "Processing":
                return new[] { "processing", "đang chuẩn bị", "đang xử lý", "chờ xử lý" };
            case "Shipping":
                return new[] { "shipping", "đang giao", "vận chuyển" };
            case "Completed":
                return new[] { "completed", "delivered", "hoàn tất", "hoàn thành" };
            case "Cancelled":
                return new[] { "cancelled", "canceled", "đã huỷ", "đã hủy" };
            default:
                return Array.Empty<string>();
        }
    }

    private static int StatusRank(string s) => s switch
    {
        "Pending" => 0,
        "Processing" => 1,
        "Shipping" => 2,
        "Completed" => 3,
        _ => -1
    };
    private static string ViOrderLabel(string s) => s switch
    {
        "Pending" => "Chờ xác nhận",
        "Processing" => "Đang chuẩn bị",
        "Shipping" => "Đang giao",
        "Completed" => "Hoàn tất",
        "Cancelled" => "Đã huỷ",
        _ => "Không rõ"
    };

    // ================== LIST + SEARCH + FILTER + PAGINATION ==================
    public async Task<IActionResult> Index(
        string? search,
        string? orderStatus,
        string? paymentStatus,
        int page = 1)
    {
        const int pageSize = 10;

        var q = _ctx.Orders
            .Include(o => o.OrderDetails)
            .AsQueryable();

        // ---- Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(o =>
                EF.Functions.Like(o.OrderId.ToString(), $"%{search}%") ||
                EF.Functions.Like(o.RecipientName ?? "", $"%{search}%") ||
                EF.Functions.Like(o.RecipientPhone ?? "", $"%{search}%") ||
                EF.Functions.Like(o.DeliveryAddress ?? "", $"%{search}%") ||
                EF.Functions.Like(o.Note ?? "", $"%{search}%")
            );
        }

        // ---- Filter OrderStatus (EN & VI) - KHÔNG GỌI HÀM TRONG WHERE
        if (!string.IsNullOrWhiteSpace(orderStatus))
        {
            var statusSet = OrderStatusAliasesLower(orderStatus);
            if (statusSet.Length > 0)
            {
                q = q.Where(o => statusSet.Contains((o.OrderStatus ?? "").ToLower()));
            }
            else
            {
                q = q.Where(o => false);
            }
        }

        // ---- Filter PaymentStatus (EN & VI)
        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            var paymentSet = PaymentAliasesLower(paymentStatus);
            if (paymentSet.Length > 0)
            {
                q = q.Where(o => paymentSet.Contains((o.PaymentStatus ?? "").ToLower()));
            }
            else
            {
                q = q.Where(o => false);
            }
        }

        // ---- Tổng theo bộ lọc hiện tại
        var filteredTotal = await q.CountAsync();

        // ---- Sắp xếp: mới nhất lên đầu
        q = q.OrderByDescending(o => o.OrderDate ?? DateTime.MinValue);

        // ---- Phân trang 10/trang
        var totalPages = (int)Math.Ceiling(filteredTotal / (double)pageSize);
        page = Math.Max(1, (totalPages == 0 ? 1 : Math.Min(page, totalPages)));

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // ---- Thống kê (dùng alias set + ToLower để EF translate)
        var pendingSet = OrderStatusAliasesLower("Pending");
        var paidSet = PaymentAliasesLower("Paid");
        var unpaidSet = PaymentAliasesLower("Unpaid");

        ViewBag.TotalOrders = await _ctx.Orders.CountAsync();
        ViewBag.PendingCount = await _ctx.Orders.CountAsync(o => pendingSet.Contains((o.OrderStatus ?? "").ToLower()));
        ViewBag.PaidCount = await _ctx.Orders.CountAsync(o => paidSet.Contains((o.PaymentStatus ?? "").ToLower()));
        ViewBag.UnpaidCount = await _ctx.Orders.CountAsync(o => unpaidSet.Contains((o.PaymentStatus ?? "").ToLower()));

        // ---- Phân trang info
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = totalPages;
        ViewBag.FilteredTotal = filteredTotal;

        return View(items);
    }

    // ================== DETAILS ==================
    public async Task<IActionResult> Details(int id)
    {
        var order = await _ctx.Orders
            .Include(o => o.User)
            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Product)
            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Color)
            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Size)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();

        decimal itemsSubtotal = order.OrderDetails?.Sum(od =>
            (od.TotalPrice ?? (od.UnitPrice ?? 0m) * (od.Quantity ?? 0))) ?? 0m;

        ViewBag.ItemsSubtotal = itemsSubtotal;
        ViewBag.ShippingFee = order.ShippingFee ?? 0m;

        var baseTotal = order.TotalAmount ?? itemsSubtotal;
        ViewBag.GrandTotal = baseTotal + (order.ShippingFee ?? 0m);

        return View(order);
    }

    // ================== EDIT ==================
    public class EditOrderVm
    {
        public int OrderId { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Note { get; set; }
        public int? PromoCode { get; set; }
        public string? PaymentStatus { get; set; }
        public string? OrderStatus { get; set; }
        public string? ShippingFee { get; set; }
        public List<LineVm> Lines { get; set; } = new();
    }
    public class LineVm
    {
        public int OrderDetailId { get; set; }
        public int? Quantity { get; set; }
        public string? UnitPrice { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await _ctx.Orders
            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Product)
            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Color)
            .Include(o => o.OrderDetails).ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Size)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditOrderVm vm)
    {
        var order = await _ctx.Orders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderId == vm.OrderId);

        if (order == null) return NotFound();

        var errors = new List<string>();

        var currentStatus = NormalizeStatus(order.OrderStatus);
        var targetStatus = NormalizeStatus(vm.OrderStatus);
        var targetPayment = NormalizePayment(vm.PaymentStatus);

        if (targetStatus == "Unknown") errors.Add("Trạng thái đơn không hợp lệ.");
        if (targetPayment == "Unknown") errors.Add("Trạng thái thanh toán không hợp lệ.");

        if (targetStatus != "Cancelled" && StatusRank(targetStatus) < StatusRank(currentStatus))
            errors.Add($"Không thể cập nhật lùi trạng thái từ “{ViOrderLabel(currentStatus)}” về “{ViOrderLabel(targetStatus)}”.");

        if (targetStatus == "Cancelled")
        {
            if (currentStatus is "Completed" or "Cancelled")
                errors.Add("Đơn đã hoàn tất hoặc đã huỷ, không thể thay đổi.");
            else if (currentStatus is not ("Pending" or "Processing"))
                errors.Add("Chỉ được huỷ khi đơn đang ở trạng thái “Chờ xác nhận” hoặc “Đang chuẩn bị”.");
        }

        if (targetStatus == "Completed" && targetPayment != "Paid")
            errors.Add("Chỉ được chuyển sang “Hoàn tất” khi trạng thái thanh toán là “Đã thanh toán”.");

        decimal shippingFee = 0m;
        if (!string.IsNullOrWhiteSpace(vm.ShippingFee) &&
            !decimal.TryParse(vm.ShippingFee, NumberStyles.Any, CultureInfo.InvariantCulture, out shippingFee))
        {
            errors.Add("Phí vận chuyển không hợp lệ.");
        }

        decimal itemsSubtotal = 0m;
        for (int i = 0; i < vm.Lines.Count; i++)
        {
            var l = vm.Lines[i];
            var line = order.OrderDetails.FirstOrDefault(d => d.OrderDetailId == l.OrderDetailId);
            if (line == null) continue;

            line.Quantity = l.Quantity ?? 0;

            decimal unit = 0m;
            if (!string.IsNullOrWhiteSpace(l.UnitPrice) &&
                !decimal.TryParse(l.UnitPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out unit))
            {
                errors.Add($"Dòng #{i + 1}: Đơn giá không hợp lệ.");
            }
            line.UnitPrice = unit;
            line.TotalPrice = unit * (line.Quantity ?? 0);
            itemsSubtotal += line.TotalPrice ?? 0m;
        }

        if (errors.Any())
        {
            TempData["ErrorMessage"] = string.Join("<br/>", errors);
            return RedirectToAction(nameof(Edit), new { id = vm.OrderId });
        }

        order.RecipientName = vm.RecipientName;
        order.RecipientPhone = vm.RecipientPhone;
        order.DeliveryAddress = vm.DeliveryAddress;
        order.Note = vm.Note;
        order.PromoCode = vm.PromoCode;

        order.OrderStatus = targetStatus;   // Pending/Processing/Shipping/Completed/Cancelled
        order.PaymentStatus = targetPayment; // Paid/Unpaid/Pending

        order.ShippingFee = shippingFee;
        order.TotalAmount = itemsSubtotal;
        order.Quantity = order.OrderDetails.Sum(d => d.Quantity ?? 0);

        await _ctx.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cập nhật đơn hàng thành công!";
        return RedirectToAction(nameof(Details), new { id = order.OrderId });
    }

    // Heartbeat giữ nguyên
    [HttpGet]
    public IActionResult Heartbeat()
    {
        var last = _ctx.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.OrderDate ?? DateTime.MinValue)
            .Select(o => new { o.OrderId, o.OrderStatus, o.PaymentStatus, o.OrderDate })
            .FirstOrDefault();

        if (last == null) return Json(new { ok = true, fp = "none" });

        var stamp = last.OrderDate ?? DateTime.MinValue;
        var fp = $"{last.OrderId}|{last.OrderStatus}|{last.PaymentStatus}|{stamp:O}";
        return Json(new { ok = true, fp });
    }
}
