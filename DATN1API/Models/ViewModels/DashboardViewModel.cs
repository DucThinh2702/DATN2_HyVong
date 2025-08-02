namespace DATN1API.Models.ViewModels
{
    public class DashboardViewModel
    {
        public decimal DoanhThuThangNay { get; set; }
        public int DonHangMoi { get; set; }
        public int SoKhachHangMoi { get; set; }
        public int TongSanPhamTrongKho { get; set; }

        public List<DonHangDto>? DonHangGanDay { get; set; }
        public List<SanPhamBanChayDto>? SanPhamBanChay { get; set; }

        public List<string>? LabelsDoanhThu { get; set; }
        public List<decimal>? DataDoanhThu { get; set; }
        public List<int>? DataDonHang { get; set; }
    }

    public class DonHangDto
    {
        public string? MaDon { get; set; }
        public string? TenKhachHang { get; set; }
        public decimal TongTien { get; set; }
        public string? TrangThai { get; set; }
    }

    public class SanPhamBanChayDto
    {
        public string? TenSanPham { get; set; }
        public string? DanhMuc { get; set; }
        public int SoLuongBan { get; set; }
        public decimal DoanhThu { get; set; }
    }

}
