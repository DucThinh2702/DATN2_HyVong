using DATN1API.Data;
using DATN1API.Models.ViewModels;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DATN1API.Controllers
{
    public class CustomerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // GET: /Customer/ChiTietKhachHang/{id}
        public async Task<IActionResult> ChiTietKhachHang(string id)
        {
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
                Status = user.Status // <-- cái này cần CustomerViewModel có Status

            };

            return View(model); // ✅ Model chính xác với View
        }


        // GET: /Customer/SuaKhachHang/{id}
        public async Task<IActionResult> SuaKhachHang(string id)
        {
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
                Status = user.Status // <-- cái này cần CustomerViewModel có Status

            };

            return View(model); // ✅ View nhận đúng kiểu
        }


        // POST: /Customer/SuaKhachHang/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuaKhachHang(CustomerViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.PhoneNumber = model.Phone;
            user.Address = model.Address;
            user.Gender = model.Gender;
            user.Status = model.Status; // <-- cái này cần CustomerViewModel có Status

            if (model.DateOfBirth.HasValue)
                user.BirthDate = model.DateOfBirth.Value;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Cập nhật khách hàng thành công!";
                return RedirectToAction("KhachHang", "Admin");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }
    }
}
