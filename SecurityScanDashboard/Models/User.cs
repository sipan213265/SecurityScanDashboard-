using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecurityScanDashboard.Models
{
    [Table("users", Schema = "public")]
    public class User
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("first_name")]
        public string? FirstName { get; set; }

        [MaxLength(255)]
        [Column("last_name")]
        public string? LastName { get; set; }

        [MaxLength(50)]
        [Column("user_type")]
        public string? UserType { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("profile_image_url")]
        public string? ProfileImageUrl { get; set; }

        [MaxLength(20)]
        [Column("phone")]
        public string? Phone { get; set; }

        [Column("is_email_verified")]
        public bool IsEmailVerified { get; set; } = false;

        [Column("email_verified_at")]
        public DateTime? EmailVerifiedAt { get; set; }

        [NotMapped]
        public string? PasswordResetToken { get; set; }

        [NotMapped]
        public DateTime? PasswordResetExpires { get; set; }

        [MaxLength(10)]
        [Column("language_code")]
        public string? LanguageCode { get; set; } = "tr";

        [Column("create_date")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("update_date")]
        public DateTime UpdateDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        
        // Computed property for full name
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
