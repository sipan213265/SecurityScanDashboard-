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
        public DbSet<Project> Projects { get; set; }
        public DbSet<Repository> Repositories { get; set; }
        public DbSet<Scan> Scans { get; set; }
        public DbSet<Vulnerability> Vulnerabilities { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }

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
            modelBuilder.Entity<Project>().ToTable("Projects", "belek_appsec");
            modelBuilder.Entity<Repository>().ToTable("Repositories", "belek_appsec");
            modelBuilder.Entity<Scan>().ToTable("Scans", "belek_appsec");
            modelBuilder.Entity<Vulnerability>().ToTable("Vulnerabilities", "belek_appsec");

            // Project configuration
            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.HasOne(p => p.Owner)
                    .WithMany()
                    .HasForeignKey(p => p.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Repository configuration
            modelBuilder.Entity<Repository>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Url);
                entity.Property(e => e.Url).IsRequired();
                entity.Property(e => e.OwnerId).IsRequired();

                entity.HasOne(r => r.RepositoryOwner)
                    .WithMany()
                    .HasForeignKey(r => r.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Project)
                    .WithMany(p => p.Repositories)
                    .HasForeignKey(r => r.ProjectId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
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

                entity.Property(e => e.Severity).HasConversion<string>();
                entity.Property(e => e.ValidationStatus).HasConversion<string>();
            });

            // AppSettings table in belek_appsec schema
            modelBuilder.Entity<AppSetting>(entity =>
            {
                entity.ToTable("AppSettings", "belek_appsec");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).IsRequired().HasMaxLength(200).HasColumnName("Key");
                entity.Property(e => e.Value).HasMaxLength(2000).HasColumnName("Value");
                entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            });
        }
    }
}
