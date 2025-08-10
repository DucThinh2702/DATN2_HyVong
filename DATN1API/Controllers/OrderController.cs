using DATN1API.Data;
using DATN1API.Models;
using DATN1API.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly DatnContext _context;

        public OrderController(DatnContext context)
        {
            _context = context;
        }

        
        // POST: api/order
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CheckoutRequest request)
        {
            try
            {
                var totalAmount = request.Items.Sum(x => x.UnitPrice * x.Quantity);

                // Kiểm tra từng variantId có tồn tại
                foreach (var item in request.Items)
                {
                    var variant = await _context.ProductVariants.FindAsync(item.ProductVariantId);
                    if (variant == null)
                    {
                        return BadRequest("Thông tin sản phẩm không hợp lệ.");
                    }
                }

                var order = new Order
                {
                    UserId = request.UserId,
                    OrderDate = DateTime.Now,
                    OrderStatus = "Pending",
                    PaymentStatus = "Unpaid",
                    Quantity = request.Items.Sum(x => x.Quantity),
                    TotalAmount = totalAmount + (request.ShippingFee ?? 0),
                    ShippingFee = request.ShippingFee,
                    PromoCode = request.PromoCode,
                    RecipientName = request.RecipientName,
                    RecipientPhone = request.RecipientPhone,
                    DeliveryAddress = request.DeliveryAddress,
                    Note = request.Note,
                    OrderDetails = request.Items.Select(x => new OrderDetail
                    {
                        ProductVariantId = x.ProductVariantId,
                        Quantity = x.Quantity,
                        UnitPrice = x.UnitPrice,
                        TotalPrice = x.UnitPrice * x.Quantity
                    }).ToList()
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                return Ok(order);
            }
            catch (Exception ex)
            {
                // Ghi log (nếu cần) hoặc trả về lỗi
                return StatusCode(500, $"Lỗi máy chủ khi tạo đơn hàng: {ex.Message}");
            }
        }



        // GET: api/order
        [HttpGet]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductVariant)
                .Include(o => o.User)
                .Include(o => o.Promotion)
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/order/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.ProductVariant)
                .Include(o => o.User)
                .Include(o => o.Promotion)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            return Ok(order);
        }

        // PUT: api/order/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] string newStatus)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound();

            order.OrderStatus = newStatus;
            await _context.SaveChangesAsync();
            return Ok(order);
        }

        // DELETE: api/order/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            _context.OrderDetails.RemoveRange(order.OrderDetails);
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Deleted successfully" });
        }
    }
}
