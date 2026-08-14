using Microsoft.EntityFrameworkCore;
using CivicFix.Api.Models;

namespace CivicFix.Api.Data
{
    public class AppDbContext : DbContext //declare context class inherting from EF class(DBContext)
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }//The constructor. Receives configuration options (database address, SQL Server settings, NetTopologySuite) that were registered in Program.cs

        public DbSet<User> Users { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Municipality> Municipalities { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<StatusHistory> StatusHistories { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<ReportAssignment> ReportAssignments { get; set; }
        public DbSet<PasswordReset> PasswordResets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)//A special method EF Core calls automatically when generating migrations
        {//ModelBuilder is the tool we use to configure how entities map to database tables.

            // define primary keys for  entities
            modelBuilder.Entity<Municipality>().HasKey(m => m.mun_Id);
            modelBuilder.Entity<User>().HasKey(u => u.usr_Id);
            modelBuilder.Entity<Category>().HasKey(c => c.ctg_Id);
            modelBuilder.Entity<Report>().HasKey(r => r.rpt_Id);
            modelBuilder.Entity<ReportAssignment>().HasKey(ra => ra.rpa_Id);
            modelBuilder.Entity<StatusHistory>().HasKey(s => s.sth_Id);
            modelBuilder.Entity<Comment>().HasKey(c => c.cmt_Id);
            modelBuilder.Entity<PasswordReset>().HasKey(p => p.pwr_Id);

            // cascade delete rules
            // Comment → Report
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Report)   // each Comment belongs to one Report
                .WithMany()               // each Report has many Comments its empty because we didnt use something like to see the collection of coment for a spcefic user
                .HasForeignKey(c => c.cmt_ReportId) // FK is cmt_ReportId
                .OnDelete(DeleteBehavior.Restrict);

            // StatusHistory → User (who changed the status)
            modelBuilder.Entity<StatusHistory>()
                .HasOne(s => s.ChangedBy)  // each StatusHistory has one User who changed it
                .WithMany()
                .HasForeignKey(s => s.sth_ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // StatusHistory → Report
            modelBuilder.Entity<StatusHistory>()
                .HasOne(s => s.Report)
                .WithMany()
                .HasForeignKey(s => s.sth_ReportId)
                .OnDelete(DeleteBehavior.Restrict);

            // ReportAssignment → Report
            modelBuilder.Entity<ReportAssignment>()
                .HasOne(ra => ra.Report)
                .WithMany()
                .HasForeignKey(ra => ra.rpa_ReportId)
                .OnDelete(DeleteBehavior.Restrict);

            // ReportAssignment → Municipality
            modelBuilder.Entity<ReportAssignment>()
                .HasOne(ra => ra.Municipality)
                .WithMany()
                .HasForeignKey(ra => ra.rpa_MunicipalityId)
                .OnDelete(DeleteBehavior.Restrict);

            // PasswordReset → User
            modelBuilder.Entity<PasswordReset>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.pwr_UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}