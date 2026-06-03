using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecurityScanDashboard.Models
{
    public enum ScanType
    {
        SAST,
        DAST
    }

    public enum ScanStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    public class Scan
    {
        public int Id { get; set; }

        public int RepositoryId { get; set; }

        [Required]
        public ScanType Type { get; set; }

        [Required]
        public ScanStatus Status { get; set; } = ScanStatus.Pending;

        [MaxLength(100)]
        public string? ToolName { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public string? ErrorMessage { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        // Navigation properties
        public Repository Repository { get; set; } = null!;
        public ICollection<Vulnerability> Vulnerabilities { get; set; } = new List<Vulnerability>();
    }
}
