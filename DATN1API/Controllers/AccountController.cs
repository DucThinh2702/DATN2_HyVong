using DATN1API.Data;
using DATN1API.Models.ViewModels;
using DATN1WEB.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Threading.Tasks;

namespace DATN1API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly DatnContext _context;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, DatnContext context, RoleManager<ApplicationRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;

        }
        // API lấy tất cả các vai trò
        [HttpGet("roles")]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            return Ok(roles);
        }

        // API tạo vai trò mới
        [HttpPost("create-role")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.RoleName))
            {
                return BadRequest(new { message = "Tên vai trò không thể trống." });
            }

            var roleExists = await _roleManager.RoleExistsAsync(model.RoleName);
            if (roleExists)
            {
                return BadRequest(new { message = "Vai trò này đã tồn tại." });
            }

            var role = new ApplicationRole { Name = model.RoleName };
            var result = await _roleManager.CreateAsync(role);

            if (result.Succeeded)
            {
                return Ok(new { message = "Vai trò đã được tạo thành công." });
            }

            return BadRequest(new { message = "Tạo vai trò thất bại." });
        }

        // API lấy tất cả người dùng kèm theo vai trò
        [HttpGet("users-with-roles")]
        public async Task<IActionResult> GetUsersWithRoles()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRoles = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles.Add(new
                {
                    user.Id,
                    user.UserName,
                    Roles = roles
                });
            }

            return Ok(userRoles);
        }

        // Cập nhật vai trò của người dùng
        [HttpPut("update-role")]
        public async Task<IActionResult> UpdateUserRole([FromBody] UpdateUserRoleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Người dùng không tồn tại." });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);

            // Xóa các vai trò cũ của người dùng
            foreach (var role in currentRoles)
            {
                await _userManager.RemoveFromRoleAsync(user, role);
            }

            // Thêm vai trò mới cho người dùng
            var addRoleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (addRoleResult.Succeeded)
            {
                return Ok(new { message = "Cập nhật vai trò người dùng thành công." });
            }

            return BadRequest(new { message = "Cập nhật vai trò không thành công." });
        }



        // Đăng ký người dùng mới
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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

                // Gửi email xác thực
                await SendEmailAsync(user.Email, "Xác nhận tài khoản", $"Click vào đây để xác nhận tài khoản của bạn: {confirmationLink}");

                return Ok(new { message = "Đăng ký thành công, vui lòng kiểm tra email để xác nhận tài khoản." });
            }

            return BadRequest(result.Errors);
        }

        // Xác nhận tài khoản qua email
        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User không tồn tại." });
            }

            // Xác thực email với token
            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                // Cập nhật trạng thái tài khoản đã xác thực
                user.Status = true;
                await _context.SaveChangesAsync();

                // Trả về kết quả thành công
                return Ok(new { message = "Tài khoản đã được xác thực thành công." });
            }

            return BadRequest(new { message = "Xác thực không thành công." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, false);
                if (result.Succeeded)
                {
                    // Sau khi đăng nhập thành công, bạn có thể lấy thông tin người dùng tại đây
                    var currentUser = await _userManager.GetUserAsync(User);

                    // Trả về thông tin người dùng (ví dụ: FullName và Email) cho client
                    var userInfo = new
                    {
                        userId = currentUser.Id,
                        userName = currentUser.UserName,
                        fullName = currentUser.FullName,
                        email = currentUser.Email
                    };

                    return Ok(new { message = "Đăng nhập thành công.", user = userInfo });
                }
                else
                {
                    return Unauthorized(new { message = "Mật khẩu không chính xác." });
                }
            }

            return NotFound(new { message = "Tài khoản không tồn tại." });
        }


        // Đăng xuất người dùng
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { message = "Đăng xuất thành công." });
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
    }
}
