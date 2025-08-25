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

    // ===== Helper chuẩn hoá =====
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

    private static string[] PaymentAliases(string? s)
    {
        switch (NormalizePayment(s))
        {
            case "Paid":
                return new[] { "Paid", "Đã thanh toán", "da thanh toan", "Đã thanh toán" };
            case "Unpaid":
                return new[] { "Unpaid", "Chưa thanh toán", "chua thanh toan" };
            case "Pending":
                return new[] { "Pending", "Chờ thanh toán", "cho thanh toan" };
            default:
                return Array.Empty<string>();
        }
    }

    // ================== LIST ==================
    public async Task<IActionResult> Index(string search, string orderStatus, string paymentStatus)
    {
        var q = _ctx.Orders
            .Include(o => o.OrderDetails)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(o =>
                o.OrderId.ToString().Contains(search) ||
                (o.RecipientName ?? "").Contains(search) ||
                (o.RecipientPhone ?? "").Contains(search) ||
                (o.DeliveryAddress ?? "").Contains(search) ||
                (o.Note ?? "").Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(orderStatus))
            q = q.Where(o => o.OrderStatus == orderStatus);

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            // Lọc theo tập alias để khớp EN & VI trong DB
            var aliases = PaymentAliases(paymentStatus);
            if (aliases.Length > 0)
                q = q.Where(o => aliases.Contains(o.PaymentStatus!));
            else
                q = q.Where(o => false); // Không hợp lệ => không trả kết quả
        }

        ViewBag.TotalOrders = await _ctx.Orders.CountAsync();
        ViewBag.PendingCount = await _ctx.Orders.CountAsync(o => o.OrderStatus == "Pending");
        ViewBag.PaidCount = await _ctx.Orders.CountAsync(o => PaymentAliases("Paid").Contains(o.PaymentStatus!));
        ViewBag.UnpaidCount = await _ctx.Orders.CountAsync(o => PaymentAliases("Unpaid").Contains(o.PaymentStatus!));

        var data = await q.OrderByDescending(o => o.OrderDate ?? DateTime.MinValue)
                          .Take(500)
                          .ToListAsync();

        return View(data);
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
        order.PaymentStatus = targetPayment;  // Paid/Unpaid/Pending

        order.ShippingFee = shippingFee;
        order.TotalAmount = itemsSubtotal;
        order.Quantity = order.OrderDetails.Sum(d => d.Quantity ?? 0);

        await _ctx.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cập nhật đơn hàng thành công!";
        return RedirectToAction(nameof(Details), new { id = order.OrderId });
    }

    // Heartbeat giữ nguyên (không dùng UpdatedAt)
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
