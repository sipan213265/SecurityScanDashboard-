using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecurityScanDashboard.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Proje adı zorunludur")]
        [MaxLength(200, ErrorMessage = "Proje adı en fazla 200 karakter olabilir")]
        [Display(Name = "Proje Adı")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        [Display(Name = "Açıklama")]
        public string? Description { get; set; }

        [Required]
        public int OwnerId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public User? Owner { get; set; }
        public ICollection<Repository> Repositories { get; set; } = new List<Repository>();
    }
}
