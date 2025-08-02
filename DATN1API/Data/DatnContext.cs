
using DATN1API.Models;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DATN1API.Data
{
    public class DatnContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public DatnContext(DbContextOptions<DatnContext> options)
            : base(options)
        {
        }


        public virtual DbSet<Cart> Carts { get; set; }

        public virtual DbSet<CartDetail> CartDetails { get; set; }

        public virtual DbSet<Category> Categories { get; set; }

        public virtual DbSet<News> News { get; set; }

        public virtual DbSet<Order> Orders { get; set; }

        public virtual DbSet<OrderDetail> OrderDetails { get; set; }

        public virtual DbSet<Payment> Payments { get; set; }

        public virtual DbSet<Product> Products { get; set; }

        public virtual DbSet<Promotion> Promotions { get; set; }
        public virtual DbSet<ProductVariant> ProductVariants { get; set; }
        public virtual DbSet<Color> Colors { get; set; }
        public virtual DbSet<Size> Sizes { get; set; }
        public DbSet<ShippingProvider> ShippingProviders { get; set; }
    }
}