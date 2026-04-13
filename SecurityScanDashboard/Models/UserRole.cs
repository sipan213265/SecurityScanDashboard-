using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecurityScanDashboard.Models
{
    [Table("user_roles", Schema = "public")]
    public class UserRole
    {
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("role_id")]
        public int RoleId { get; set; }

        [Column("create_date")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("update_date")]
        public DateTime? UpdateDate { get; set; }

        [Column("operation_user_id")]
        public int? OperationUserId { get; set; }

        [Column("archive_action")]
        public string? ArchiveAction { get; set; }

        [Column("archive_date")]
        public DateTime? ArchiveDate { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; } = null!;
    }
}
