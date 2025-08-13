using DATN1API.Data;
using DATN1API.Models;
using DATN1API.Models.ViewModels;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace DATN1API.Controllers
{
    public class AdminController : Controller
    {
        private readonly DatnContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(DatnContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var model = new DashboardViewModel
            {
                DoanhThuThangNay = await _context.Orders
                    .Where(o => o.OrderDate.HasValue && o.OrderDate.Value >= startOfMonth && o.OrderStatus == "Delivered")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0,

                DonHangMoi = await _context.Orders
                    .CountAsync(o => o.OrderDate.HasValue && o.OrderDate.Value >= DateTime.Today.AddDays(-7)),

                // Lấy số lượng người dùng mới trong 7 ngày qua
                SoKhachHangMoi = await _userManager.Users
                    .Where(u => u.UserName != null) // Tìm tất cả người dùng
                    .CountAsync(),




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

                SanPhamBanChay = await _context.OrderDetails
                    .Include(od => od.ProductVariant) // Bao gồm ProductVariant
                    .ThenInclude(pv => pv.Product)   // Sau đó bao gồm Product từ ProductVariant
                    .ThenInclude(p => p.Category)    // Tiếp tục bao gồm Category từ Product
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

                LabelsDoanhThu = Enumerable.Range(0, 7)
                    .Select(i => DateTime.Today.AddDays(-6 + i).ToString("dd/MM"))
                    .ToList()
            };

            // 🛠 Fix chạy tuần tự (KHÔNG dùng Task.WhenAll)
            var dataDoanhThu = new List<decimal>();
            var dataDonHang = new List<int>();

            for (int i = 0; i < 7; i++)
            {
                var date = DateTime.Today.AddDays(-6 + i);

                var total = await _context.Orders
                    .Where(o => o.OrderDate.HasValue && o.OrderDate.Value.Date == date.Date && o.OrderStatus == "Delivered")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

                var count = await _context.Orders
                    .Where(o => o.OrderDate.HasValue && o.OrderDate.Value.Date == date.Date)
                    .CountAsync();

                dataDoanhThu.Add(total);
                dataDonHang.Add(count);
            }

            model.DataDoanhThu = dataDoanhThu;
            model.DataDonHang = dataDonHang;

            return View(model);
        }

        public IActionResult DanhMuc()
        {
            return View();
        }

        public async Task<IActionResult> MaGiamGia(string status = "Tất cả", string search = "")
        {
            var today = DateTime.Today;

            // Lấy danh sách mã giảm giá và xử lý null tại đây
            var danhSach = await _context.Promotions
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
                .AsNoTracking()
                .ToListAsync();

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

            return View(danhSach);
        }

        public async Task<IActionResult> KhachHang()
        {
            var users = (await _userManager.Users.ToListAsync())
                .Where(u => !_userManager.IsInRoleAsync(u, "Admin").Result)
                .Select(u => new CustomerViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName ?? u.UserName,
                    Email = u.Email,
                    Phone = u.PhoneNumber,
                    Address = u.Address,
                    Gender = u.Gender,
                    DateOfBirth = u.BirthDate,
                    Status = u.Status // bool
                })
                .ToList();

            var totalCustomers = users.Count;
            var verifiedCustomers = users.Count(u => u.Status == true);   // Đã xác thực
            var unverifiedCustomers = users.Count(u => u.Status == false); // Chưa xác thực
            var activeCustomers = users.Count(u => !string.IsNullOrEmpty(u.Email));

            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.VerifiedCustomers = verifiedCustomers;
            ViewBag.UnverifiedCustomers = unverifiedCustomers;
            ViewBag.ActiveCustomers = activeCustomers;

            return View(users);
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
