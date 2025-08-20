
    public class OrderInfo
    {
        public long OrderId { get; set; }           // Mã đơn hàng (duy nhất)
        public int Amount { get; set; }             // Số tiền thanh toán (VND)
        public string? Status { get; set; }          // Trạng thái thanh toán: 0 = chờ, 1 = thành công, 2 = thất bại
        public string? OrderDesc { get; set; }       // Mô tả đơn hàng
        public DateTime CreatedDate { get; set; }   // Ngày tạo đơn hàng

        // (Tuỳ chọn - dùng nếu bạn lưu thêm thông tin người nhận)
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
    }

