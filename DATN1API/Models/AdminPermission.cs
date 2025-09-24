namespace DATN1API.Models
{
    public class AdminPermission
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;   // liên kết ApplicationUser
        public string FunctionName { get; set; } = string.Empty; // Ví dụ: "Product", "Order", "Voucher"
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanView { get; set; }
    }

}
