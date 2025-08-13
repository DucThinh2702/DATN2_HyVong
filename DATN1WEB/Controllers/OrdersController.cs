using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;

public class OrdersController : Controller
{
    private readonly DatnContext _ctx;
    public OrdersController(DatnContext ctx) => _ctx = ctx;

    public async Task<IActionResult> Index(string search, string orderStatus, string paymentStatus)
    {
        var q = _ctx.Orders
            .Include(o => o.OrderDetails) // để tính SL
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            q = q.Where(o =>
                o.OrderId.ToString().Contains(search) ||
                (o.RecipientName ?? "").Contains(search) ||
                (o.RecipientPhone ?? "").Contains(search) ||
                (o.DeliveryAddress ?? "").Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(orderStatus))
            q = q.Where(o => o.OrderStatus == orderStatus);

        if (!string.IsNullOrWhiteSpace(paymentStatus))
            q = q.Where(o => o.PaymentStatus == paymentStatus);

        ViewBag.TotalOrders = await _ctx.Orders.CountAsync();
        ViewBag.PendingCount = await _ctx.Orders.CountAsync(o => o.OrderStatus == "Pending");
        ViewBag.PaidCount = await _ctx.Orders.CountAsync(o => o.PaymentStatus == "Paid");
        ViewBag.UnpaidCount = await _ctx.Orders.CountAsync(o => o.PaymentStatus == "Unpaid");

        var data = await q.OrderByDescending(o => o.OrderDate)
                          .Take(500)
                          .ToListAsync();

        return View(data);
    }
    public async Task<IActionResult> Details(int id)
    {
        var order = await _ctx.Orders
            .Include(o => o.User)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.ProductVariant)
                    .ThenInclude(pv => pv.Product)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.ProductVariant)
                    .ThenInclude(pv => pv.Color)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.ProductVariant)
                    .ThenInclude(pv => pv.Size)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();

        // Tính lại subtotal an toàn từ chi tiết
        decimal itemsSubtotal = order.OrderDetails?.Sum(od =>
            (od.TotalPrice ?? (od.UnitPrice ?? 0m) * (od.Quantity ?? 0))) ?? 0m;

        ViewBag.ItemsSubtotal = itemsSubtotal;
        ViewBag.ShippingFee = order.ShippingFee ?? 0m;

        // Nếu TotalAmount của bạn đã là tổng hàng, dùng cái đó; nếu không thì dùng itemsSubtotal
        var baseTotal = order.TotalAmount ?? itemsSubtotal;
        ViewBag.GrandTotal = baseTotal + (order.ShippingFee ?? 0m);

        return View(order);
    }
    // ViewModel cho POST Edit
    public class EditOrderVm
    {
        public int OrderId { get; set; }

        // Header
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Note { get; set; }
        public string? PromoCode { get; set; }
        public string? PaymentStatus { get; set; }  // "Paid" / "Unpaid"
        public string? OrderStatus { get; set; }    // "Pending" / "Processing" / "Completed" / "Cancelled"
        public string? ShippingFee { get; set; }    // string để tự parse InvariantCulture

        // Lines
        public List<LineVm> Lines { get; set; } = new();
    }

    public class LineVm
    {
        public int OrderDetailId { get; set; }
        public int? Quantity { get; set; }
        public string? UnitPrice { get; set; } // string để tự parse InvariantCulture
    }

    // ============== EDIT (GET) ==============
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var order = await _ctx.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.ProductVariant)
                    .ThenInclude(pv => pv.Product)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.ProductVariant)
                    .ThenInclude(pv => pv.Color)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.ProductVariant)
                    .ThenInclude(pv => pv.Size)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();

        return View(order); // View mạnh kiểu Order, form sẽ post theo tên field của EditOrderVm
    }

    // ============== EDIT (POST) ==============
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditOrderVm vm)
    {
        var order = await _ctx.Orders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderId == vm.OrderId);

        if (order == null) return NotFound();

        // Parse ShippingFee an toàn
        decimal shippingFee = 0m;
        if (!string.IsNullOrWhiteSpace(vm.ShippingFee))
        {
            if (!decimal.TryParse(vm.ShippingFee, NumberStyles.Any, CultureInfo.InvariantCulture, out shippingFee))
            {
                ModelState.AddModelError("ShippingFee", "Phí vận chuyển không hợp lệ.");
            }
        }

        // Parse & cập nhật từng dòng
        decimal itemsSubtotal = 0m;
        foreach (var l in vm.Lines)
        {
            var line = order.OrderDetails.FirstOrDefault(d => d.OrderDetailId == l.OrderDetailId);
            if (line == null) continue;

            // SL
            line.Quantity = l.Quantity ?? 0;

            // Đơn giá
            decimal unit = 0m;
            if (!string.IsNullOrWhiteSpace(l.UnitPrice))
            {
                if (!decimal.TryParse(l.UnitPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out unit))
                {
                    ModelState.AddModelError($"Lines[{vm.Lines.IndexOf(l)}].UnitPrice", "Đơn giá không hợp lệ.");
                }
            }
            line.UnitPrice = unit;

            // Thành tiền
            line.TotalPrice = unit * (line.Quantity ?? 0);

            itemsSubtotal += line.TotalPrice ?? 0m;
        }

        if (!ModelState.IsValid)
        {
            // Re-load quan hệ để render lại view kèm lỗi
            order = await _ctx.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Color)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductVariant).ThenInclude(pv => pv.Size)
                .FirstOrDefaultAsync(o => o.OrderId == vm.OrderId)!;

            return View(order);
        }

        // Cập nhật header
        order.RecipientName = vm.RecipientName;
        order.RecipientPhone = vm.RecipientPhone;
        order.DeliveryAddress = vm.DeliveryAddress;
        order.Note = vm.Note;
        order.PromoCode = vm.PromoCode;

        // Trạng thái
        order.PaymentStatus = vm.PaymentStatus; // "Paid" / "Unpaid"
        order.OrderStatus = vm.OrderStatus;   // "Pending" / "Processing" / "Completed" / "Cancelled"

        // Totals
        order.ShippingFee = shippingFee;
        order.TotalAmount = itemsSubtotal;                 // tổng hàng (chưa ship)
        order.Quantity = order.OrderDetails.Sum(d => d.Quantity ?? 0);

        await _ctx.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cập nhật đơn hàng thành công!";
        return RedirectToAction(nameof(Details), new { id = order.OrderId });
    }
}
