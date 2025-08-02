using DATN1API.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace DATN1API.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
        [StringLength(100, MinimumLength = 4, ErrorMessage = "Tên đăng nhập phải có độ dài từ 4 đến 100 ký tự.")]
        [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Tên đăng nhập chỉ được chứa chữ và số.")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [CheckEmailUnique(ErrorMessage = "Email đã tồn tại.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Số điện thoại phải gồm 10 chữ số.")]
        [CheckPhoneNumberUnique(ErrorMessage = "Số điện thoại đã tồn tại.")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Ngày sinh là bắt buộc.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [MinimumAge(15, ErrorMessage = "Bạn phải đủ 15 tuổi để đăng ký.")]
        public DateTime? BirthDate { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có độ dài từ 6 đến 100 ký tự.")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
        [Compare("Password", ErrorMessage = "Mật khẩu và xác nhận mật khẩu không khớp.")]
        public string? ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Giới tính là bắt buộc.")]
        public string? Gender { get; set; }

        [Required(ErrorMessage = "Địa chỉ là bắt buộc.")]
        [StringLength(225, ErrorMessage = "Địa chỉ không được dài quá 225 ký tự.")]
        public string? Address { get; set; }

        public DateTime? CreatedDate { get; set; }
    }

    #region Custom Validation Attributes

    // Kiểm tra trùng email trong cơ sở dữ liệu
    public class CheckEmailUniqueAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var dbContext = (DatnContext)validationContext.GetService(typeof(DatnContext));

            var email = value?.ToString();
            if (dbContext.Users.Any(u => u.Email == email))
            {
                return new ValidationResult("Email đã tồn tại.");
            }

            return ValidationResult.Success;
        }
    }

    // Kiểm tra trùng số điện thoại trong cơ sở dữ liệu
    public class CheckPhoneNumberUniqueAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var dbContext = (DatnContext)validationContext.GetService(typeof(DatnContext));

            var phoneNumber = value?.ToString();
            if (dbContext.Users.Any(u => u.PhoneNumber == phoneNumber))
            {
                return new ValidationResult("Số điện thoại đã tồn tại.");
            }

            return ValidationResult.Success;
        }
    }

    // Kiểm tra độ tuổi tối thiểu
    public class MinimumAgeAttribute : ValidationAttribute
    {
        private readonly int _minimumAge;
        public MinimumAgeAttribute(int minimumAge)
        {
            _minimumAge = minimumAge;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is DateTime birthDate)
            {
                var age = DateTime.Now.Year - birthDate.Year;
                if (age < _minimumAge)
                {
                    return new ValidationResult($"Bạn phải đủ {_minimumAge} tuổi để đăng ký.");
                }
            }

            return ValidationResult.Success;
        }
    }

    #endregion
}
