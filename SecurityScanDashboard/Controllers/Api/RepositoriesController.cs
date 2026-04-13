using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.DTOs;
using SecurityScanDashboard.Models;
using SecurityScanDashboard.Services;
using Hangfire;

namespace SecurityScanDashboard.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Produces("application/json")]
    public class RepositoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IGitHubService _gitHubService;
        private readonly ILogger<RepositoriesController> _logger;

        public RepositoriesController(
            ApplicationDbContext context,
            IGitHubService gitHubService,
            ILogger<RepositoriesController> logger)
        {
            _context = context;
            _gitHubService = gitHubService;
            _logger = logger;
        }

        /// <summary>
        /// Get all repositories with pagination
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<RepositoryDto>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var totalCount = await _context.Repositories.CountAsync();
            
            var repositories = await _context.Repositories
                .Include(r => r.Scans)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RepositoryDto
                {
                    Id = r.Id,
                    Name = r.Name ?? "",
                    Url = r.Url,
                    Owner = r.Owner ?? "",
                    LiveUrl = r.LiveUrl,
                    CreatedAt = r.CreatedAt,
                    TotalScans = r.Scans.Count,
                    CompletedScans = r.Scans.Count(s => s.Status == ScanStatus.Completed),
                    TotalVulnerabilities = r.Scans
                        .Where(s => s.Status == ScanStatus.Completed)
                        .SelectMany(s => s.Vulnerabilities)
                        .Count()
                })
                .ToListAsync();

            var paginatedData = new PaginatedResponse<RepositoryDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = repositories
            };

            return Ok(ApiResponse<PaginatedResponse<RepositoryDto>>.SuccessResponse(paginatedData));
        }

        /// <summary>
        /// Get repository by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<RepositoryDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(int id)
        {
            var repository = await _context.Repositories
                .Include(r => r.Scans)
                .ThenInclude(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (repository == null)
            {
                return NotFound(ApiResponse<RepositoryDto>.ErrorResponse($"Repository with ID {id} not found"));
            }

            var dto = new RepositoryDto
            {
                Id = repository.Id,
                Name = repository.Name ?? "",
                Url = repository.Url,
                Owner = repository.Owner ?? "",
                LiveUrl = repository.LiveUrl,
                CreatedAt = repository.CreatedAt,
                TotalScans = repository.Scans.Count,
                CompletedScans = repository.Scans.Count(s => s.Status == ScanStatus.Completed),
                TotalVulnerabilities = repository.Scans
                    .Where(s => s.Status == ScanStatus.Completed)
                    .SelectMany(s => s.Vulnerabilities)
                    .Count()
            };

            return Ok(ApiResponse<RepositoryDto>.SuccessResponse(dto));
        }

        /// <summary>
        /// Create a new repository
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<RepositoryDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody] CreateRepositoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest(ApiResponse<RepositoryDto>.ErrorResponse("Repository URL is required"));
            }

            // Check if repository already exists
            var existingRepo = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Url == request.Url);

            if (existingRepo != null)
            {
                return BadRequest(ApiResponse<RepositoryDto>.ErrorResponse("Repository already exists"));
            }

            try
            {
                // Parse GitHub URL to extract owner and name
                var uri = new Uri(request.Url);
                var pathParts = uri.AbsolutePath.Trim('/').Split('/');
                var owner = pathParts.Length > 0 ? pathParts[0] : "Unknown";
                var name = pathParts.Length > 1 ? pathParts[1].Replace(".git", "") : uri.Host;

                var repository = new Repository
                {
                    Url = request.Url,
                    Name = name,
                    Owner = owner,
                    LiveUrl = request.LiveUrl,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Repositories.Add(repository);
                await _context.SaveChangesAsync();

                var dto = new RepositoryDto
                {
                    Id = repository.Id,
                    Name = repository.Name ?? "",
                    Url = repository.Url,
                    Owner = repository.Owner ?? "",
                    LiveUrl = repository.LiveUrl,
                    CreatedAt = repository.CreatedAt,
                    TotalScans = 0,
                    CompletedScans = 0,
                    TotalVulnerabilities = 0
                };

                _logger.LogInformation($"Repository created via API: {repository.Url}");

                return CreatedAtAction(nameof(GetById), new { id = repository.Id }, 
                    ApiResponse<RepositoryDto>.SuccessResponse(dto, "Repository created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating repository via API");
                return BadRequest(ApiResponse<RepositoryDto>.ErrorResponse("Invalid repository URL"));
            }
        }

        /// <summary>
        /// Delete a repository
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(int id)
        {
            var repository = await _context.Repositories.FindAsync(id);

            if (repository == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse($"Repository with ID {id} not found"));
            }

            _context.Repositories.Remove(repository);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Repository deleted via API: {repository.Url}");

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Repository deleted successfully"));
        }
    }
}
