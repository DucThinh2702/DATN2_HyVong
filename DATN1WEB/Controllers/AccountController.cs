
using DATN1API.Data;
using DATN1API.Models.ViewModels;
using DATN1WEB.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace DATN1API.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly DatnContext _context;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, DatnContext context, RoleManager<ApplicationRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
        }

        // Trang đăng ký
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // Xử lý đăng ký
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,  // Email sẽ là Username
                    Email = model.Email,
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber,
                    Gender = model.Gender,
                    Address = model.Address,
                    BirthDate = model.BirthDate,
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Gán vai trò mặc định cho người dùng (Role = 2: User)
                    await _userManager.AddToRoleAsync(user, "User");

                    // Gửi email xác thực
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token = token }, Request.Scheme);

                    await SendEmailAsync(user.Email, "Xác nhận tài khoản", $"Click vào đây để xác nhận tài khoản của bạn: {confirmationLink}");


                    // Chuyển hướng tới view xác nhận email
                    return RedirectToAction("ConfirmEmailNotification", new { userId = user.Id });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // Xác nhận email khi người dùng nhấp vào liên kết trong email
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                // Cập nhật trạng thái tài khoản đã xác thực
                user.Status = true;
                await _context.SaveChangesAsync();

                return RedirectToAction("Login");
            }

            return RedirectToAction("Index", "Home");
        }

        // Trang đăng nhập
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // Xử lý đăng nhập
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && user.Status)
                {
                    var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, false);
                    if (result.Succeeded)
                    {
                        // Lấy vai trò của người dùng
                        var roles = await _userManager.GetRolesAsync(user);

                        // Lưu thông tin người dùng vào session hoặc UserClaims (Nếu cần lưu lâu dài trong phiên)
                        var userData = new
                        {
                            user.FullName,
                            user.Email,
                            Roles = roles
                        };
                        HttpContext.Session.SetString("UserData", JsonConvert.SerializeObject(userData));

                        if (roles.Contains("Admin"))
                        {
                            return RedirectToAction("Index", "Admin");
                        }
                        else if (roles.Contains("User"))
                        {
                            return RedirectToAction("Index", "User");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Mật khẩu không chính xác.");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Tài khoản không tồn tại hoặc chưa được xác thực.");
                }
            }

            return View(model);
        }


        // Đăng xuất
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Đảm bảo đăng xuất khỏi Identity
            await _signInManager.SignOutAsync();

            // Xóa tất cả dữ liệu session (nếu cần)
            HttpContext.Session.Clear();

            // Đảm bảo không thể quay lại trang trước khi đăng nhập
            return RedirectToAction("Login", "Account");
        }

        // Gửi email xác thực
        private async Task SendEmailAsync(string email, string subject, string message)
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("NoReply", "no-reply@example.com"));
            emailMessage.To.Add(new MailboxAddress("", email));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart("plain")
            {
                Text = message
            };

            using (var smtp = new SmtpClient())
            {
                await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync("aklaakthoi9@gmail.com", "drwt fawk ybao kpdz"); // Dùng mật khẩu ứng dụng nếu xác minh 2 bước bật
                await smtp.SendAsync(emailMessage);
                await smtp.DisconnectAsync(true);
            }
        }

        // Trang thông báo xác nhận email
        [HttpGet]
        public IActionResult ConfirmEmailNotification()
        {
            return View();
        }
    }
}
