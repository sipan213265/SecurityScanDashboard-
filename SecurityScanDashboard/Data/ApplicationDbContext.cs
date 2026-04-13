using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Models;

namespace SecurityScanDashboard.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Application tables (belek_appsec schema)
        public DbSet<Repository> Repositories { get; set; }
        public DbSet<Scan> Scans { get; set; }
        public DbSet<Vulnerability> Vulnerabilities { get; set; }

        // Authentication tables (public schema)
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            
            // Migration History tablosu: "__EFMigrationsHistory" yerine "EFMigrationsHistory"
            // Çünkü okul iki alt çizgiyi kabul etmiyor
            optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IHistoryRepository, CustomHistoryRepository>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure authentication tables in public schema (EXCLUDED from migrations - managed by school)
            modelBuilder.Entity<User>().ToTable("users", "public", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<User>().Property(u => u.Id).ValueGeneratedOnAdd(); // School table - GENERATED ALWAYS AS IDENTITY
            modelBuilder.Entity<Role>().ToTable("roles", "public", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserRole>().ToTable("user_roles", "public", t => t.ExcludeFromMigrations());

            // Configure UserRole composite key
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            // Configure UserRole relationships
            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Application tables in belek_appsec schema
            modelBuilder.Entity<Repository>().ToTable("Repositories", "belek_appsec");
            modelBuilder.Entity<Scan>().ToTable("Scans", "belek_appsec");
            modelBuilder.Entity<Vulnerability>().ToTable("Vulnerabilities", "belek_appsec");

            // Repository configuration
            modelBuilder.Entity<Repository>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Url);
                entity.Property(e => e.Url).IsRequired();
                entity.Property(e => e.OwnerId).IsRequired();
                
                // Relationship with User
                entity.HasOne(r => r.RepositoryOwner)
                    .WithMany()
                    .HasForeignKey(r => r.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Scan configuration
            modelBuilder.Entity<Scan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.StartedAt);
                
                entity.HasOne(s => s.Repository)
                    .WithMany(r => r.Scans)
                    .HasForeignKey(s => s.RepositoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Type)
                    .HasConversion<string>();
                
                entity.Property(e => e.Status)
                    .HasConversion<string>();
            });

            // Vulnerability configuration
            modelBuilder.Entity<Vulnerability>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Severity);
                entity.HasIndex(e => e.DetectedAt);
                
                entity.HasOne(v => v.Scan)
                    .WithMany(s => s.Vulnerabilities)
                    .HasForeignKey(v => v.ScanId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Severity)
                    .HasConversion<string>();
            });
        }
    }
}
