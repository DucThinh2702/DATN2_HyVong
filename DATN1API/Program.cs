
using DATN1API.Data;
using DATN1API.Models.Pay;
using DATN1API.Pay;
using DATN1API.Services;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Cấu hình DbContext cho API
builder.Services.AddDbContext<DatnContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình Identity cho API
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

builder.Services.Configure<VNPAYSettings>(builder.Configuration.GetSection("VNPAY"));
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<VnPayService>(); // Đăng ký VnPayService

// Cấu hình session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);  // Thời gian hết hạn session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cấu hình các dịch vụ khác
builder.Services.AddControllers();

// Cấu hình Swagger cho API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Seed roles and users
using (var scope = builder.Services.BuildServiceProvider().CreateScope())  // Build service provider here
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    await SeedRoles.CreateRoles(services, userManager, roleManager);
}
builder.Services.AddDistributedMemoryCache();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

// Đảm bảo sử dụng session
app.UseSession();  // Đặt sau `UseHttpsRedirection` và trước `UseRouting`

app.UseAuthorization();

// Cấu hình các route cho API Controllers
app.MapControllers();

app.Run();
