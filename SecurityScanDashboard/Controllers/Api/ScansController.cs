using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.DTOs;
using SecurityScanDashboard.Models;
using SecurityScanDashboard.Jobs;
using Hangfire;

namespace SecurityScanDashboard.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Produces("application/json")]
    public class ScansController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ScansController> _logger;

        public ScansController(
            ApplicationDbContext context,
            ILogger<ScansController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all scans with pagination
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<ScanDto>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var totalCount = await _context.Scans.CountAsync();

            var scans = await _context.Scans
                .Include(s => s.Repository)
                .Include(s => s.Vulnerabilities)
                .OrderByDescending(s => s.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                .ToListAsync();

            var paginatedData = new PaginatedResponse<ScanDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = scans
            };

            return Ok(ApiResponse<PaginatedResponse<ScanDto>>.SuccessResponse(paginatedData));
        }

        /// <summary>
        /// Get scan by ID with vulnerabilities
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<ScanDetailDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(int id)
        {
            var scan = await _context.Scans
                .Include(s => s.Repository)
                .Include(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (scan == null)
            {
                return NotFound(ApiResponse<ScanDetailDto>.ErrorResponse($"Scan with ID {id} not found"));
            }

            var dto = new ScanDetailDto
            {
                Id = scan.Id,
                RepositoryId = scan.RepositoryId,
                RepositoryName = scan.Repository.Name ?? "",
                Type = scan.Type.ToString(),
                ToolName = scan.ToolName ?? "",
                Status = scan.Status.ToString(),
                StartedAt = scan.StartedAt,
                CompletedAt = scan.CompletedAt,
                ErrorMessage = scan.ErrorMessage,
                VulnerabilityCount = scan.Vulnerabilities.Count,
                Vulnerabilities = scan.Vulnerabilities.Select(v => new VulnerabilityDto
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
                }).ToList()
            };

            return Ok(ApiResponse<ScanDetailDto>.SuccessResponse(dto));
        }

        /// <summary>
        /// Start a new scan
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ScanDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> StartScan([FromBody] StartScanRequest request)
        {
            // Validate repository exists
            var repository = await _context.Repositories.FindAsync(request.RepositoryId);
            if (repository == null)
            {
                return BadRequest(ApiResponse<ScanDto>.ErrorResponse("Repository not found"));
            }

            // Validate scan type
            if (!Enum.TryParse<ScanType>(request.ScanType, true, out var scanType))
            {
                return BadRequest(ApiResponse<ScanDto>.ErrorResponse("Invalid scan type. Must be SAST or DAST"));
            }

            // Validate DAST requirements
            if (scanType == ScanType.DAST && string.IsNullOrWhiteSpace(repository.LiveUrl))
            {
                return BadRequest(ApiResponse<ScanDto>.ErrorResponse("Live URL is required for DAST scans"));
            }

            var scan = new Scan
            {
                RepositoryId = request.RepositoryId,
                Type = scanType,
                Status = ScanStatus.Pending,
                ToolName = request.Tool,
                StartedAt = DateTime.UtcNow
            };

            _context.Scans.Add(scan);
            await _context.SaveChangesAsync();

            // Queue the scan job
            if (scanType == ScanType.SAST)
            {
                BackgroundJob.Enqueue<ScanJob>(job => job.ExecuteSastScanAsync(scan.Id, request.Tool));
            }
            else
            {
                BackgroundJob.Enqueue<ScanJob>(job => job.ExecuteDastScanAsync(scan.Id));
            }

            _logger.LogInformation($"Scan started via API: Type={scanType}, Tool={request.Tool}, Repository={repository.Name}");

            var dto = new ScanDto
            {
                Id = scan.Id,
                RepositoryId = scan.RepositoryId,
                RepositoryName = repository.Name,
                Type = scan.Type.ToString(),
                ToolName = scan.ToolName,
                Status = scan.Status.ToString(),
                StartedAt = scan.StartedAt,
                VulnerabilityCount = 0
            };

            return CreatedAtAction(nameof(GetById), new { id = scan.Id },
                ApiResponse<ScanDto>.SuccessResponse(dto, "Scan started successfully"));
        }

        /// <summary>
        /// Start a quick DAST scan without creating repository
        /// </summary>
        [HttpPost("quick")]
        [ProducesResponseType(typeof(ApiResponse<ScanDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> QuickScan([FromBody] QuickScanRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TargetUrl))
            {
                return BadRequest(ApiResponse<ScanDto>.ErrorResponse("Target URL is required"));
            }

            if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest(ApiResponse<ScanDto>.ErrorResponse("Invalid URL format"));
            }

            // Create temporary repository
            var repository = new Repository
            {
                Url = request.TargetUrl,
                Name = uri.Host,
                Owner = "Quick Scan",
                LiveUrl = request.TargetUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.Repositories.Add(repository);
            await _context.SaveChangesAsync();

            var scan = new Scan
            {
                RepositoryId = repository.Id,
                Type = ScanType.DAST,
                Status = ScanStatus.Pending,
                ToolName = "Nuclei",
                StartedAt = DateTime.UtcNow
            };

            _context.Scans.Add(scan);
            await _context.SaveChangesAsync();

            // Queue the DAST scan job
            BackgroundJob.Enqueue<ScanJob>(job => job.ExecuteDastScanAsync(scan.Id));

            _logger.LogInformation($"Quick DAST scan started via API: {request.TargetUrl}");

            var dto = new ScanDto
            {
                Id = scan.Id,
                RepositoryId = scan.RepositoryId,
                RepositoryName = repository.Name,
                Type = scan.Type.ToString(),
                ToolName = scan.ToolName,
                Status = scan.Status.ToString(),
                StartedAt = scan.StartedAt,
                VulnerabilityCount = 0
            };

            return CreatedAtAction(nameof(GetById), new { id = scan.Id },
                ApiResponse<ScanDto>.SuccessResponse(dto, "Quick scan started successfully"));
        }
    }
}
