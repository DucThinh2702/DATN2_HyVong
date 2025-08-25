namespace DATN1API.Models.ViewModels
{
    public class CustomerViewModel
    {
        public string? Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; } // << dùng DateTime? thay vì string
        public bool Status { get; set; }
        // thuộc tính mới
        public int OrdersCount { get; set; }      // số đơn
        public decimal TotalSpent { get; set; }
        public int CancelledCount { get; set; }
        public int UnpaidCount { get; set; }
        public DateTime? CreatedAt { get; set; }

    }

}
