using System.ComponentModel.DataAnnotations;
using DATN1API.Models.ViewModels;
namespace DATN1API.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có độ dài từ 6 đến 100 ký tự.")]
        public string? Password { get; set; }
        public bool RememberMe { get; set; }
    }
}
