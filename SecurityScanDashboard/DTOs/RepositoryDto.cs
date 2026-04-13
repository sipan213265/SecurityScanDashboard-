namespace SecurityScanDashboard.DTOs
{
    public class RepositoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Owner { get; set; }
        public string? LiveUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalScans { get; set; }
        public int CompletedScans { get; set; }
        public int TotalVulnerabilities { get; set; }
    }

    public class CreateRepositoryRequest
    {
        public string Url { get; set; } = string.Empty;
        public string? LiveUrl { get; set; }
    }
}
