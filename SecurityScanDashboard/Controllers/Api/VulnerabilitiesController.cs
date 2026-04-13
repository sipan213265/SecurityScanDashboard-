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
    public class VulnerabilitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VulnerabilitiesController> _logger;

        public VulnerabilitiesController(
            ApplicationDbContext context,
            ILogger<VulnerabilitiesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all vulnerabilities with pagination and filtering
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<VulnerabilityDto>>), 200)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? severity = null,
            [FromQuery] int? scanId = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _context.Vulnerabilities.AsQueryable();

            // Filter by severity
            if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<SeverityLevel>(severity, true, out var severityLevel))
            {
                query = query.Where(v => v.Severity == severityLevel);
            }

            // Filter by scan ID
            if (scanId.HasValue)
            {
                query = query.Where(v => v.ScanId == scanId.Value);
            }

            var totalCount = await query.CountAsync();

            var vulnerabilities = await query
                .OrderByDescending(v => v.DetectedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new VulnerabilityDto
                {
                    Id = v.Id,
                    ScanId = v.ScanId,
                    Title = v.Title,
                    Description = v.Description,
                    Severity = v.Severity.ToString(),
                    FilePath = v.FilePath,
                    LineNumber = v.LineNumber,
                    CweId = v.CweId,
                    CveId = v.CveId,
                    DetectedAt = v.DetectedAt
                })
                .ToListAsync();

            var paginatedData = new PaginatedResponse<VulnerabilityDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = vulnerabilities
            };

            return Ok(ApiResponse<PaginatedResponse<VulnerabilityDto>>.SuccessResponse(paginatedData));
        }

        /// <summary>
        /// Get vulnerability statistics
        /// </summary>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(ApiResponse<VulnerabilityStatsDto>), 200)]
        public async Task<IActionResult> GetStats()
        {
            var stats = new VulnerabilityStatsDto
            {
                Total = await _context.Vulnerabilities.CountAsync(),
                Critical = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Critical),
                High = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.High),
                Medium = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Medium),
                Low = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Low)
            };

            return Ok(ApiResponse<VulnerabilityStatsDto>.SuccessResponse(stats));
        }
    }
}
