namespace SecurityScanDashboard.DTOs
{
    public class DashboardDto
    {
        public int TotalRepositories { get; set; }
        public int TotalScans { get; set; }
        public int RunningScans { get; set; }
        public VulnerabilityStatsDto Vulnerabilities { get; set; } = new();
        public List<ScanDto> RecentScans { get; set; } = new();
    }
}
