using Microsoft.EntityFrameworkCore;
using CivicFix.Api.Models;

namespace CivicFix.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Municipality> Municipalities { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<StatusHistory> StatusHistories { get; set; }
        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // disable cascade delete on all relationships to avoid multiple cascade paths
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Report)
                .WithMany()
                .HasForeignKey(c => c.ReportId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StatusHistory>()
                .HasOne(s => s.ChangedBy)
                .WithMany()
                .HasForeignKey(s => s.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StatusHistory>()
                .HasOne(s => s.Report)
                .WithMany()
                .HasForeignKey(s => s.ReportId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.SecondaryMunicipality)
                .WithMany()
                .HasForeignKey(r => r.SecondaryMunicipalityId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}