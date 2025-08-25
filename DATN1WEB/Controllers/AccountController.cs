using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Newtonsoft.Json;
using DATN1API.Data;
using DATN1API.Models.ViewModels;
using DATN1WEB.Models;

namespace DATN1API.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly DatnContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            DatnContext context,
            RoleManager<ApplicationRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
        }

        // ========= REGISTER =========
        [HttpGet, AllowAnonymous]
        public IActionResult Register() => View();

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                Gender = model.Gender,
                Address = model.Address,
                BirthDate = model.BirthDate,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var link = Url.Action("ConfirmEmail", "Account",
                    new { userId = user.Id, token }, Request.Scheme);

                await SendEmailAsync(user.Email!,
                    "Xác nhận tài khoản",
                    $"Click vào đây để xác nhận tài khoản của bạn: {link}");

                return RedirectToAction("ConfirmEmailNotification", new { userId = user.Id });
            }

            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }

        // ========= CONFIRM EMAIL =========
        [HttpGet, AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return RedirectToAction("Index", "Home");

            var ok = await _userManager.ConfirmEmailAsync(user, token);
            if (ok.Succeeded)
            {
                user.Status = true;
                await _userManager.UpdateAsync(user); // cập nhật qua Identity cho đúng
                return RedirectToAction("Login");
            }
            return RedirectToAction("Index", "Home");
        }

        // ========= LOGIN DÙNG CHUNG (User + Admin theo returnUrl) =========
        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl; // để view bơm vào input hidden
            return View();
        }

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !user.Status)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại hoặc chưa được xác thực.");
                return View(model);
            }

            var pwd = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: false);
            if (!pwd.Succeeded)
            {
                ModelState.AddModelError("", "Mật khẩu không chính xác.");
                return View(model);
            }

            // Kiểm tra role
            var roles = await _userManager.GetRolesAsync(user);
            var isAdmin = roles.Contains("Admin");

            // Nếu user không phải Admin mà returnUrl trỏ vào /Admin => báo lỗi như cũ
            if (!isAdmin && IsAdminReturnUrl(returnUrl))
            {
                ModelState.AddModelError("", "Tài khoản không có quyền Admin.");
                return View(model);
            }

            // 1) Luôn phát cookie USER (Identity)
            await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);

            // 2) Nếu là Admin => phát thêm cookie ADMIN (để vào /Admin không phải login lại)
            if (isAdmin)
            {
                var principal = await _signInManager.CreateUserPrincipalAsync(user);
                await HttpContext.SignInAsync("AdminScheme", principal, new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : null
                });
            }

            // 3) Điều hướng
            if (Url.IsLocalUrl(returnUrl)) // dùng helper chuẩn của MVC
                return Redirect(returnUrl!);

            // Không có returnUrl: Admin về Admin, User về User
            return isAdmin
                ? RedirectToAction("Index", "Admin")
                : RedirectToAction("Index", "User");
        }


        // ========= LOGOUT =========
        // Đăng xuất riêng phiên USER
        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutUser()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return LocalRedirect("~/Account/Login"); // hoặc "~/"
                                                     // hoặc RedirectToAction("Login","Account", new { }, Request.Scheme);
        }


        // Đăng xuất riêng phiên ADMIN
        [Authorize(Policy = "IsAdmin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutAdmin()
        {
            await HttpContext.SignOutAsync("AdminScheme"); // AdminAuthCookie
            return RedirectToAction("Login", new { returnUrl = "/Admin" });
        }

        // Đăng xuất cả hai (nếu cần 1 nút tổng)
        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutAll()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await HttpContext.SignOutAsync("AdminScheme");
            return RedirectToAction("Login");
        }

        [HttpGet, AllowAnonymous]
        public IActionResult AccessDenied() => View();

        [HttpGet, AllowAnonymous]
        public IActionResult ConfirmEmailNotification() => View();

        // ========= HELPERS =========
        private static bool IsSafeLocalUrl(string? url)
            => !string.IsNullOrEmpty(url) && Uri.IsWellFormedUriString(url, UriKind.Relative);

        private static bool IsAdminReturnUrl(string? url)
            => !string.IsNullOrEmpty(url) && url.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase);

        private async Task SendEmailAsync(string email, string subject, string message)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("NoReply", "no-reply@example.com"));
            msg.To.Add(new MailboxAddress("", email));
            msg.Subject = subject;
            msg.Body = new TextPart("plain") { Text = message };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync("aklaakthoi9@gmail.com", "drwt fawk ybao kpdz"); // đổi sang App Password của bạn
            await smtp.SendAsync(msg);
            await smtp.DisconnectAsync(true);
        }
        [HttpGet, AllowAnonymous]
        public IActionResult AdminEntry() => RedirectToAction("Login", new { returnUrl = "/Admin" });

        [HttpGet, AllowAnonymous]
        public IActionResult UserEntry() => RedirectToAction("Login");

    }
}
