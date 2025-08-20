
using DATN1API.Data;
using DATN1API.Pay;
using DATN1API.Services;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using DATN1API.Models.Pay;


var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext cho ứng dụng và Identity
builder.Services.AddDbContext<DatnContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging()); // Kích hoạt Sensitive Data Logging nếu cần
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";          // nơi redirect khi chưa đăng nhập
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ReturnUrlParameter = "returnUrl";
});

// Cấu hình Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Các tùy chọn cấu hình cho Identity
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
    options.SignIn.RequireConfirmedEmail = true; // Bật xác nhận email nếu cần
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<DatnContext>()
.AddDefaultTokenProviders();
builder.Services.Configure<PayOSOptions>(builder.Configuration.GetSection("PayOS"));

builder.Services.AddHttpClient<PayOSService>(client =>
{
    var opt = builder.Configuration.GetSection("PayOS").Get<PayOSOptions>();
    if (!string.IsNullOrWhiteSpace(opt?.BaseUrl))
        client.BaseAddress = new Uri(opt.BaseUrl.Trim());  // ← nhớ Trim

    if (!string.IsNullOrEmpty(opt?.ClientId))
        client.DefaultRequestHeaders.Add("X-Client-Id", opt.ClientId);
    if (!string.IsNullOrEmpty(opt?.ApiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", opt.ApiKey);
});

// ❌ XÓA dòng AddScoped<PayOSService>();

builder.Services.Configure<PayOSOptions>(builder.Configuration.GetSection("PayOS"));
builder.Services.Configure<VNPAYSettings>(builder.Configuration.GetSection("VNPAY"));
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<VnPayService>(); // Đăng ký VnPayService

// Cấu hình session nếu bạn dùng OTP hoặc giữ thông tin tạm
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10); // Thời gian hết hạn của session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cấu hình dịch vụ MVC
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Cấu hình các dịch vụ khác
builder.Services.AddRazorPages().AddViewOptions(options =>
{
    options.HtmlHelperOptions.ClientValidationEnabled = true;
});
builder.Services.AddHttpClient("api", client =>
{
    // Đặt URL API cho HttpClient (đúng port dự án API)
    client.BaseAddress = new Uri("https://localhost:7138/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Cấu hình các dịch vụ middleware
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Cấu hình HSTS cho môi trường production
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Đảm bảo sử dụng session để lưu OTP
app.UseSession();

// Sử dụng Routing và Middleware cho Authentication và Authorization
app.UseRouting();

// ======= Thêm middleware chống cache cho toàn site =======
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    ctx.Response.Headers["Pragma"] = "no-cache";
    ctx.Response.Headers["Expires"] = "0";
    await next();
});
// Thêm xác thực và phân quyền
app.UseAuthentication();  // Thêm middleware cho xác thực
app.UseAuthorization();   // Thêm middleware cho phân quyền

// Cấu hình các route cho Controller
app.MapControllerRoute(
    name: "default",
//pattern: "{controller=SanPham}/{action=Index}/{id?}");
pattern: "{controller=User}/{action=Index}/{id?}");

app.Run();
