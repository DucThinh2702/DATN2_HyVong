
using DATN1API.Data;
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
