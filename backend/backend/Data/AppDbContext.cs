using Microsoft.EntityFrameworkCore;
using Oracle.EntityFrameworkCore.Infrastructure;
using backend.Models.Data;

namespace backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<AppUser> AppUsers { get; set; } = null!;
        public DbSet<AuthAuditLog> AuthAuditLogs { get; set; } = null!;
        public DbSet<Student> Students { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique indexes for username and email
            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // No foreign key constraint from AuthAuditLog to AppUser,
            // so audit records survive independently of the user row.
            modelBuilder.Entity<AuthAuditLog>()
                .Property(a => a.UserId)
                .IsRequired(false);
        }
    }
}