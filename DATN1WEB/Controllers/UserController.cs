using DATN1API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using DATN1API.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using DATN1WEB.Models;

namespace DATN1API.Controllers
{
    public class UserController : Controller
    {
        private readonly DatnContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserController(DatnContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        public IActionResult Index()
        {
            var products = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Where(p => p.Status.ToLower() == "active")
                .ToList();

            return View(products); // truyền danh sách sản phẩm xuống view
        }

        public IActionResult GioHang()
        {
            return View();
        }

        public IActionResult ThanhToan()
        {
            return View();
        }

        public IActionResult LienHe()
        {
            return View();
        }

        public IActionResult QuenMatKhau()
        {
            return View();
        }

        public IActionResult DangNhap()
        {
            return View();
        }

        public IActionResult DangKy()
        {
            return View();
        }

        public IActionResult ChiTiet()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> ProfileUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("DangNhap");
            }

            var userProfile = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email);

            if (userProfile == null)
            {
                // Create a new user profile if doesn't exist
                userProfile = new ApplicationUser
                {
                    Email = user.Email,
                    UserName = user.UserName
                };
            }

            return View(userProfile);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfileUser(ApplicationUser model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("DangNhap");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                ModelState.AddModelError("FullName", "Họ và tên là bắt buộc.");
            }
            else if (model.FullName.Length < 2 || model.FullName.Length > 100)
            {
                ModelState.AddModelError("FullName", "Họ và tên phải từ 2 đến 100 ký tự.");
            }

            if (string.IsNullOrWhiteSpace(model.UserName))
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập là bắt buộc.");
            }
            else if (model.UserName.Length < 3 || model.UserName.Length > 50)
            {
                ModelState.AddModelError("UserName", "Tên đăng nhập phải từ 3 đến 50 ký tự.");
            }

            // Validate phone number
            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                var phoneRegex = new Regex(@"^(0[3|5|7|8|9])+([0-9]{8})$");
                if (!phoneRegex.IsMatch(model.PhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam.");
                }
                else
                {
                    // Check if phone number already exists (excluding current user)
                    var existingPhone = await _context.Users
                        .AnyAsync(u => u.PhoneNumber == model.PhoneNumber && u.Email != user.Email);
                    if (existingPhone)
                    {
                        ModelState.AddModelError("PhoneNumber", "Số điện thoại này đã được sử dụng.");
                    }
                }
            }

            // Validate birth date
            if (model.BirthDate.HasValue)
            {
                var age = DateTime.Now.Year - model.BirthDate.Value.Year;
                if (model.BirthDate.Value.Date > DateTime.Now.AddYears(-age)) age--;

                if (age < 15)
                {
                    ModelState.AddModelError("DateOfBirth", "Bạn phải từ 13 tuổi trở lên.");
                }
                else if (age > 120)
                {
                    ModelState.AddModelError("DateOfBirth", "Ngày sinh không hợp lệ.");
                }
            }

            // Validate address length
            if (!string.IsNullOrWhiteSpace(model.Address) && model.Address.Length > 200)
            {
                ModelState.AddModelError("Address", "Địa chỉ không được vượt quá 200 ký tự.");
            }

            // Validate gender
            if (!string.IsNullOrWhiteSpace(model.Gender) &&
                !new[] { "Nam", "Nữ", "Khác" }.Contains(model.Gender))
            {
                ModelState.AddModelError("Gender", "Giới tính không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == user.Email);

                if (existingUser != null)
                {
                    existingUser.FullName = model.FullName?.Trim();
                    existingUser.UserName = model.UserName?.Trim();
                    existingUser.PhoneNumber = model.PhoneNumber?.Trim();
                    existingUser.Address = model.Address?.Trim();
                    existingUser.BirthDate = model.BirthDate;
                    existingUser.Gender = model.Gender?.Trim();
                    existingUser.UpdatedAt = DateTime.Now;

                    _context.Users.Update(existingUser);
                }
                else
                {
                    var newUser = new ApplicationUser
                    {
                        FullName = model.FullName?.Trim(),
                        UserName = model.UserName?.Trim(),
                        Email = user.Email,
                        PhoneNumber = model.PhoneNumber?.Trim(),
                        Address = model.Address?.Trim(),
                        BirthDate = model.BirthDate,
                        Gender = model.Gender?.Trim(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Users.Add(newUser);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công!";
                return RedirectToAction("ProfileUser");
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi lưu thông tin. Vui lòng kiểm tra lại dữ liệu.");
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật thông tin: " + ex.Message;
                return View(model);
            }
        }
    }
}
