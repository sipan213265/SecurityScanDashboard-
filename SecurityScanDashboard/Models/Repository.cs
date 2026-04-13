using System.ComponentModel.DataAnnotations;
using SecurityScanDashboard.Attributes;

namespace SecurityScanDashboard.Models
{
    public class Repository
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Repository URL'si zorunludur")]
        [MaxLength(500, ErrorMessage = "URL en fazla 500 karakter olabilir")]
        [GitHubUrl(ErrorMessage = "Geçerli bir GitHub repository URL'si giriniz (örnek: https://github.com/owner/repo)")]
        [Display(Name = "Repository URL")]
        public string Url { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "URL en fazla 500 karakter olabilir")]
        [UrlValid(ErrorMessage = "Geçerli bir URL giriniz")]
        [Display(Name = "Canlı Uygulama URL")]
        public string? LiveUrl { get; set; }

        [MaxLength(200, ErrorMessage = "Repository adı en fazla 200 karakter olabilir")]
        [Display(Name = "Repository Adı")]
        public string? Name { get; set; }

        [MaxLength(200, ErrorMessage = "Sahip adı en fazla 200 karakter olabilir")]
        [Display(Name = "Sahip")]
        public string? Owner { get; set; }

        // Owner of the repository record (User ID)
        [Required]
        public int OwnerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User? RepositoryOwner { get; set; }
        public ICollection<Scan> Scans { get; set; } = new List<Scan>();
    }
}
