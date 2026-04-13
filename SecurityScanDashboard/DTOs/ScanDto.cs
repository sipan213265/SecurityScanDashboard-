using SecurityScanDashboard.Models;

namespace SecurityScanDashboard.DTOs
{
    public class ScanDto
    {
        public int Id { get; set; }
        public int RepositoryId { get; set; }
        public string RepositoryName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public int VulnerabilityCount { get; set; }
        public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
    }

    public class ScanDetailDto : ScanDto
    {
        public List<VulnerabilityDto> Vulnerabilities { get; set; } = new();
    }

    public class StartScanRequest
    {
        public int RepositoryId { get; set; }
        public string ScanType { get; set; } = string.Empty; // "SAST" or "DAST"
        public string Tool { get; set; } = string.Empty; // "Semgrep" or "Nuclei"
    }

    public class QuickScanRequest
    {
        public string TargetUrl { get; set; } = string.Empty;
    }
}
