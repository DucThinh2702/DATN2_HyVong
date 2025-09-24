//using DATN1API.Data;
//using DATN1API.Models.ViewModels;
//using DATN1WEB.Models;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using System.Threading.Tasks;

//namespace DATN1API.Controllers
//{
//    public class CustomerController : Controller
//    {
//        private readonly UserManager<ApplicationUser> _userManager;

//        public CustomerController(UserManager<ApplicationUser> userManager)
//        {
//            _userManager = userManager;
//        }

//        // GET: /Customer/ChiTietKhachHang/{id}
//        public async Task<IActionResult> ChiTietKhachHang(string id)
//        {
//            var user = await _userManager.FindByIdAsync(id);
//            if (user == null) return NotFound();

//            var model = new CustomerViewModel
//            {
//                Id = user.Id,
//                FullName = user.FullName,
//                Email = user.Email,
//                Phone = user.PhoneNumber,
//                Address = user.Address,
//                Gender = user.Gender,
//                DateOfBirth = user.BirthDate,
//                Status = user.Status // <-- cái này cần CustomerViewModel có Status

//            };

//            return View(model); // ✅ Model chính xác với View
//        }


//        // GET: /Customer/SuaKhachHang/{id}
//        public async Task<IActionResult> SuaKhachHang(string id)
//        {
//            var user = await _userManager.FindByIdAsync(id);
//            if (user == null) return NotFound();

//            var model = new CustomerViewModel
//            {
//                Id = user.Id,
//                FullName = user.FullName,
//                Email = user.Email,
//                Phone = user.PhoneNumber,
//                Address = user.Address,
//                Gender = user.Gender,
//                DateOfBirth = user.BirthDate,
//                Status = user.Status // <-- cái này cần CustomerViewModel có Status

//            };

//            return View(model); // ✅ View nhận đúng kiểu
//        }


//        // POST: /Customer/SuaKhachHang/{id}
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> SuaKhachHang(CustomerViewModel model)
//        {
//            if (!ModelState.IsValid) return View(model);

//            var user = await _userManager.FindByIdAsync(model.Id);
//            if (user == null) return NotFound();

//            user.FullName = model.FullName;
//            user.Email = model.Email;
//            user.PhoneNumber = model.Phone;
//            user.Address = model.Address;
//            user.Gender = model.Gender;
//            user.Status = model.Status; // <-- cái này cần CustomerViewModel có Status

//            if (model.DateOfBirth.HasValue)
//                user.BirthDate = model.DateOfBirth.Value;

//            var result = await _userManager.UpdateAsync(user);
//            if (result.Succeeded)
//            {
//                TempData["Success"] = "Cập nhật khách hàng thành công!";
//                return RedirectToAction("KhachHang", "Admin");
//            }

//            foreach (var error in result.Errors)
//                ModelState.AddModelError("", error.Description);

//            return View(model);
//        }
//    }
//}
using DATN1API.Data;
using DATN1API.Models.ViewModels;
using DATN1API.Services;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Controllers
{
    public class CustomerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PermissionService _permissionService;

        public CustomerController(UserManager<ApplicationUser> userManager, PermissionService permissionService)
        {
            _userManager = userManager;
            _permissionService = permissionService;
        }

        // ===================== DANH SÁCH =====================
        public async Task<IActionResult> Index(string? search)
        {
            var user = await _userManager.GetUserAsync(User);
            if (!await _permissionService.HasPermission(user, "Customer", "View"))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem khách hàng!";
                return RedirectToAction("Index", "Home");
            }

            var q = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                q = q.Where(u =>
                    (u.FullName ?? "").Contains(search) ||
                    (u.Email ?? "").Contains(search) ||
                    (u.PhoneNumber ?? "").Contains(search));
            }

            var data = await q.OrderByDescending(u => u.Id).ToListAsync();

            ViewBag.TotalCustomers = data.Count;
            ViewBag.Search = search;

            return View(data);
        }

        // ===================== CHI TIẾT =====================
        public async Task<IActionResult> ChiTietKhachHang(string id)
        {
            var current = await _userManager.GetUserAsync(User);
            if (!await _permissionService.HasPermission(current, "Customer", "View"))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem thông tin khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new CustomerViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Address = user.Address,
                Gender = user.Gender,
                DateOfBirth = user.BirthDate,
                Status = user.Status
            };

            return View(model);
        }

        // ===================== SỬA (GET) =====================
        public async Task<IActionResult> SuaKhachHang(string id)
        {
            var current = await _userManager.GetUserAsync(User);
            if (!await _permissionService.HasPermission(current, "Customer", "Edit"))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền sửa khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new CustomerViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Address = user.Address,
                Gender = user.Gender,
                DateOfBirth = user.BirthDate,
                Status = user.Status
            };

            return View(model);
        }

        // ===================== SỬA (POST) =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuaKhachHang(CustomerViewModel model)
        {
            var current = await _userManager.GetUserAsync(User);
            if (!await _permissionService.HasPermission(current, "Customer", "Edit"))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền sửa khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.PhoneNumber = model.Phone;
            user.Address = model.Address;
            user.Gender = model.Gender;
            user.Status = model.Status;

            if (model.DateOfBirth.HasValue)
                user.BirthDate = model.DateOfBirth.Value;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cập nhật khách hàng thành công!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ===================== XOÁ (GET) =====================
        public async Task<IActionResult> Delete(string id)
        {
            var current = await _userManager.GetUserAsync(User);
            if (!await _permissionService.HasPermission(current, "Customer", "Delete"))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xoá khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // ===================== XOÁ (POST) =====================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var current = await _userManager.GetUserAsync(User);
            if (!await _permissionService.HasPermission(current, "Customer", "Delete"))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xoá khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khách hàng!";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Xoá khách hàng thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Có lỗi khi xoá khách hàng!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
