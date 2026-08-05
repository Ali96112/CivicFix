using Microsoft.EntityFrameworkCore;
using CivicFix.Api.Models;

namespace CivicFix.Api.Data
{
    public class AppDbContext : DbContext// in herting from dbContect
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }//constructor

        public DbSet<User> Users { get; set; }
        public DbSet<Report> Reports { get; set; }
    }
}