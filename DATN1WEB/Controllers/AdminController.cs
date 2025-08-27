using DATN1API.Data;
using DATN1API.Models;
using DATN1API.Models.ViewModels;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace DATN1API.Controllers
{
    [Authorize(Policy = "IsAdmin")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class AdminController : Controller
    {
        private readonly DatnContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(DatnContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        // using ...


        [HttpGet]
        public async Task<IActionResult> ChartData(int days = 7)
        {
            if (days != 7 && days != 30 && days != 90) days = 7;

            // khoảng ngày [start; end)
            var end = DateTime.Today.AddDays(1);            // hết ngày hôm nay (mở)
            var start = end.AddDays(-days);                 // lùi "days" ngày

            // Lấy tất cả đơn trong khoảng
            var orders = await _context.Orders
                .Where(o => o.OrderDate.HasValue
                            && o.OrderDate.Value >= start
                            && o.OrderDate.Value < end)
                .Select(o => new
                {
                    Date = o.OrderDate!.Value.Date,
                    Amount = (decimal)(o.TotalAmount ?? 0m),
                    OrderStatus = o.OrderStatus,
                    PaymentStatus = o.PaymentStatus
                })
                .ToListAsync();

            // Doanh thu: CHỈ tính đơn đã thanh toán (Paid/Đã thanh toán) và KHÔNG tính Cancelled
            var revenueByDate = orders
                .Where(x => x.OrderStatus != "Cancelled"
                            && (x.PaymentStatus == "Paid" || x.PaymentStatus == "Đã thanh toán"))
                .GroupBy(x => x.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            // Số đơn: không tính Cancelled
            var countByDate = orders
                .Where(x => x.OrderStatus != "Cancelled")
                .GroupBy(x => x.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            // Build dãy liên tục theo ngày, điền 0 nếu thiếu
            var labels = new List<string>();
            var revenueSeries = new List<decimal>();
            var orderSeries = new List<int>();

            for (var d = start.Date; d < end.Date; d = d.AddDays(1))
            {
                labels.Add(d.ToString("dd/MM"));
                revenueSeries.Add(revenueByDate.TryGetValue(d, out var rv) ? rv : 0m);
                orderSeries.Add(countByDate.TryGetValue(d, out var ct) ? ct : 0);
            }

            return Json(new
            {
                labels,
                // client đang hiển thị "triệu VNĐ", giữ nguyên cách chia 1,000,000 ở view
                revenue = revenueSeries,
                orders = orderSeries
            });
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1); // giới hạn < đầu tháng sau

            // Tính doanh thu tháng này (chỉ tính các đơn đã thanh toán và không bị huỷ)
            var doanhThuThangNay = await _context.Orders
                .Where(o => o.OrderDate.HasValue
                            && o.OrderDate.Value >= startOfMonth
                            && o.OrderDate.Value < endOfMonth
                            && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Đã thanh toán") // Chỉ tính đơn đã thanh toán
                            && o.OrderStatus != "Cancelled") // Loại bỏ các đơn huỷ
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

            // Tính tổng số sản phẩm trong kho
            var tongSanPhamTrongKho = await _context.ProductVariants
                .SumAsync(v => (int?)v.Stock) ?? 0;

            // Lấy đơn hàng mới nhất trong ngày (chỉ lấy đơn hàng của ngày hôm nay)
            var donHangMoi = await _context.Orders
                .CountAsync(o => o.OrderDate.HasValue
                                 && o.OrderDate.Value.Date == DateTime.Today.Date // Lọc đơn hàng trong ngày hôm nay
                                 && o.OrderStatus != "Cancelled"); // Không tính đơn hàng bị huỷ

            // Cập nhật thông tin model để gửi lên view
            var model = new DashboardViewModel
            {
                DoanhThuThangNay = doanhThuThangNay,
                TongSanPhamTrongKho = tongSanPhamTrongKho,

                // Lấy số lượng đơn hàng mới trong ngày hôm nay
                DonHangMoi = donHangMoi,

                // Lấy số lượng khách hàng mới
                SoKhachHangMoi = await _userManager.Users
                    .Where(u => u.UserName != null)
                    .CountAsync(),

                // Lấy 5 đơn hàng gần đây nhất
                DonHangGanDay = await _context.Orders
                    .Include(o => o.User)
                    .Where(o => o.OrderDate.HasValue)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .Select(o => new DonHangDto
                    {
                        MaDon = $"ORD-{o.OrderId}",
                        TenKhachHang = o.User.FullName,
                        TongTien = o.TotalAmount ?? 0,
                        TrangThai = o.OrderStatus
                    }).ToListAsync(),

                // Lấy 5 sản phẩm bán chạy nhất
                SanPhamBanChay = await _context.OrderDetails
                    .Include(od => od.ProductVariant)
                    .ThenInclude(pv => pv.Product)
                    .ThenInclude(p => p.Category)
                    .GroupBy(od => new { od.ProductVariant.Product.ProductName, od.ProductVariant.Product.Category.CategoryName })
                    .Select(g => new SanPhamBanChayDto
                    {
                        TenSanPham = g.Key.ProductName,
                        DanhMuc = g.Key.CategoryName,
                        SoLuongBan = g.Sum(x => (int?)x.Quantity) ?? 0,
                        DoanhThu = g.Sum(x => (decimal?)x.TotalPrice) ?? 0
                    })
                    .OrderByDescending(x => x.SoLuongBan)
                    .Take(5)
                    .ToListAsync(),

                // Các nhãn cho biểu đồ doanh thu (hiện tại không sử dụng cho các ngày)
                LabelsDoanhThu = Enumerable.Range(0, 7)
                    .Select(i => DateTime.Today.AddDays(-6 + i).ToString("dd/MM"))
                    .ToList()
            };

            // Trả về View với model đã được cập nhật
            return View(model);
        }


        public IActionResult DanhMuc()
        {
            return View();
        }

        public async Task<IActionResult> MaGiamGia(string status = "Tất cả", string search = "", int page = 1, int pageSize = 3)
        {
            var today = DateTime.Today;

            // Lấy danh sách mã giảm giá và xử lý null tại đây
            var query = _context.Promotions
                .Select(p => new Promotion
                {
                    PromoCode = p.PromoCode,
                    PromoName = p.PromoName ?? "",
                    PromoNameCode = p.PromoNameCode ?? "",
                    PromoType = p.PromoType ?? "",
                    DiscountValue = p.DiscountValue ?? 0,
                    MinOrderAmount = p.MinOrderAmount ?? 0,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Quantity = p.Quantity ?? 0,
                    UsedQuantity = p.UsedQuantity ?? 0,
                    Description = p.Description ?? "",
                    ShippingProviderName = p.ShippingProviderName ?? ""
                })
                .AsNoTracking();

            var danhSach = await query.OrderByDescending(p => p.PromoCode).ToListAsync();

            // Trạng thái theo thời gian và số lượng
            foreach (var promo in danhSach)
            {
                if (promo.EndDate.HasValue && promo.EndDate.Value < today)
                {
                    promo.Status = "Hết hạn";
                }
                else if (promo.EndDate.HasValue && (promo.EndDate.Value - today).TotalDays <= 5)
                {
                    promo.Status = "Sắp hết hạn";
                }
                else if (promo.Quantity == promo.UsedQuantity)
                {
                    promo.Status = "Đã dùng hết";
                }
                else
                {
                    promo.Status = "Đang hoạt động";
                }
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status) && status != "Tất cả")
            {
                danhSach = danhSach.Where(p => p.Status == status).ToList();
            }

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.ToLower();
                danhSach = danhSach.Where(p =>
                    p.PromoCode.ToString().Contains(keyword) ||
                    p.PromoName.ToLower().Contains(keyword) ||
                    p.PromoNameCode.ToLower().Contains(keyword)
                ).ToList();
            }

            var totalItems = danhSach.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            var pagedList = danhSach.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Thống kê
            var tongMa = danhSach.Count;
            var daSuDung = danhSach.Sum(p => p.UsedQuantity ?? 0);
            var tongGiamGia = danhSach.Sum(p => (p.UsedQuantity ?? 0) * (p.DiscountValue ?? 0));
            var tongSoLuong = danhSach.Sum(p => p.Quantity ?? 0);
            var tyLeChuyenDoi = tongSoLuong > 0 ? ((double)daSuDung / tongSoLuong * 100).ToString("0.0") : "0";

            // Truyền lên view
            ViewBag.TongMa = tongMa;
            ViewBag.DaSuDung = daSuDung;
            ViewBag.TietKiem = tongGiamGia;
            ViewBag.ChuyenDoi = tyLeChuyenDoi;
            ViewBag.Search = search;
            ViewBag.CurrentStatus = status;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            return View(pagedList);
        }

        public async Task<IActionResult> KhachHang()
        {
            // Loại bỏ Admin
            var adminIds = (await _userManager.GetUsersInRoleAsync("Admin"))
                .Select(a => a.Id)
                .ToHashSet();

            // Lấy danh sách user (ẩn Admin), chỉ lấy trường cần thiết
            var users = await _userManager.Users
                .AsNoTracking()
                .Where(u => !adminIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    Email = u.Email,
                    Phone = u.PhoneNumber,
                    u.Address,
                    u.Gender,
                    DateOfBirth = u.BirthDate,
                    u.Status
                })
                .ToListAsync();

            // 1) Đếm số đơn (KHÔNG tính đơn Cancelled) để hiển thị cột "Đơn hàng"
            var ordersCountByUser = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId != null && o.OrderStatus != "Cancelled")
                .GroupBy(o => o.UserId!)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            // 2) Tổng chi tiêu: CHỈ các đơn đã thanh toán (Paid / Đã thanh toán), cũng không tính đơn Cancelled
            var spentByUser = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId != null
                    && o.OrderStatus != "Cancelled"
                    && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Đã thanh toán"))
                .GroupBy(o => o.UserId!)
                .Select(g => new
                {
                    UserId = g.Key,
                    Total = g.Sum(o => (o.TotalAmount ?? 0m) + (o.ShippingFee ?? 0m))
                })
                .ToDictionaryAsync(x => x.UserId, x => x.Total);

            // 3) Số đơn đã huỷ
            var cancelledByUser = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId != null && o.OrderStatus == "Cancelled")
                .GroupBy(o => o.UserId!)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            // 4) Số đơn chưa thanh toán (không tính Cancelled)
            var unpaidByUser = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId != null
                    && o.OrderStatus != "Cancelled"
                    && !(o.PaymentStatus == "Paid" || o.PaymentStatus == "Đã thanh toán"))
                .GroupBy(o => o.UserId!)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            // Map ra ViewModel
            var model = users.Select(u => new CustomerViewModel
            {
                Id = u.Id,
                FullName = string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                Address = u.Address,
                Gender = u.Gender,
                DateOfBirth = u.DateOfBirth,
                Status = u.Status,

                OrdersCount = ordersCountByUser.TryGetValue(u.Id, out var oc) ? oc : 0,
                TotalSpent = spentByUser.TryGetValue(u.Id, out var ts) ? ts : 0m,
                CancelledCount = cancelledByUser.TryGetValue(u.Id, out var cc) ? cc : 0,
                UnpaidCount = unpaidByUser.TryGetValue(u.Id, out var up) ? up : 0
            })
            .OrderByDescending(x => x.TotalSpent) // tuỳ ý sắp xếp
            .ToList();

            // Thống kê tổng quan
            ViewBag.TotalCustomers = model.Count;
            ViewBag.VerifiedCustomers = model.Count(u => u.Status);
            ViewBag.UnverifiedCustomers = model.Count(u => !u.Status);
            ViewBag.ActiveCustomers = model.Count(u => !string.IsNullOrEmpty(u.Email));

            return View(model);
        }

        public IActionResult DonHang()
        {
            return View();
        }

        public IActionResult SanPham()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Lấy danh sách role của user
            var roles = await _userManager.GetRolesAsync(user);
            string position = roles.FirstOrDefault() ?? "Không có chức vụ";

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Address = user.Address,
                Gender = user.Gender,
                DateOfBirth = user.BirthDate,
                Position = position // Lấy từ role
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ!";
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.PhoneNumber = model.Phone;
            user.Address = model.Address;
            user.Gender = model.Gender;
            user.BirthDate = model.DateOfBirth;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
                TempData["Success"] = "Cập nhật thông tin thành công!";
            else
                TempData["Error"] = "Đã có lỗi xảy ra khi cập nhật!";

            return RedirectToAction(nameof(Profile));
        }
    }
}
