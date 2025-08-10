using DATN1API.Data;
using DATN1API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
}
