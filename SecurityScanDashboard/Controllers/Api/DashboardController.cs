using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.DTOs;
using SecurityScanDashboard.Models;

namespace SecurityScanDashboard.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Produces("application/json")]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ApplicationDbContext context,
            ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get dashboard statistics and recent activity
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<DashboardDto>), 200)]
        public async Task<IActionResult> GetDashboard()
        {
            var dashboard = new DashboardDto
            {
                TotalRepositories = await _context.Repositories.CountAsync(),
                TotalScans = await _context.Scans.CountAsync(),
                RunningScans = await _context.Scans.CountAsync(s => s.Status == ScanStatus.Running),
                Vulnerabilities = new VulnerabilityStatsDto
                {
                    Total = await _context.Vulnerabilities.CountAsync(),
                    Critical = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Critical),
                    High = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.High),
                    Medium = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Medium),
                    Low = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Low)
                },
                RecentScans = await _context.Scans
                    .Include(s => s.Repository)
                    .Include(s => s.Vulnerabilities)
                    .OrderByDescending(s => s.StartedAt)
                    .Take(10)
                    .Select(s => new ScanDto
                    {
                        Id = s.Id,
                        RepositoryId = s.RepositoryId,
                        RepositoryName = s.Repository.Name ?? "",
                        Type = s.Type.ToString(),
                        ToolName = s.ToolName ?? "",
                        Status = s.Status.ToString(),
                        StartedAt = s.StartedAt,
                        CompletedAt = s.CompletedAt,
                        ErrorMessage = s.ErrorMessage,
                        VulnerabilityCount = s.Vulnerabilities.Count
                    })
                    .ToListAsync()
            };

            return Ok(ApiResponse<DashboardDto>.SuccessResponse(dashboard));
        }
    }
}
