using Microsoft.EntityFrameworkCore;
using RestApi.Models;

namespace RestApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<Product> Products { get; set; }
    }
}