using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;
using SecurityScanDashboard.Jobs;
using SecurityScanDashboard.Services;
using Hangfire;
using System.Security.Claims;

namespace SecurityScanDashboard.Controllers
{
    [Authorize]
    public class ScanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ScanController> _logger;
        private readonly IReportService _reportService;
        private readonly IPdfReportService _pdfReportService;

        public ScanController(
            ApplicationDbContext context, 
            ILogger<ScanController> logger, 
            IReportService reportService,
            IPdfReportService pdfReportService)
        {
            _context = context;
            _logger = logger;
            _reportService = reportService;
            _pdfReportService = pdfReportService;
        }

        // GET: Scan
        public async Task<IActionResult> Index()
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var scans = await _context.Scans
                .Include(s => s.Repository)
                .Include(s => s.Vulnerabilities)
                .Where(s => s.Repository.OwnerId == userId)
                .OrderByDescending(s => s.StartedAt)
                .ToListAsync();

            return View(scans);
        }

        // GET: Scan/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var scan = await _context.Scans
                .Include(s => s.Repository)
                .Include(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (scan == null)
            {
                return NotFound();
            }

            // Check ownership
            if (scan.Repository.OwnerId != userId)
            {
                TempData["ErrorMessage"] = "You don't have permission to view this scan.";
                return RedirectToAction(nameof(Index));
            }

            return View(scan);
        }

        // POST: Scan/StartSAST
        [HttpPost]
        public async Task<IActionResult> StartSAST([FromForm] int repositoryId, [FromForm] string tool)
        {
            _logger.LogInformation($"StartSAST called with repositoryId={repositoryId}, tool={tool}");
            
            try
            {
                int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
                var repository = await _context.Repositories.FindAsync(repositoryId);
                if (repository == null)
                {
                    _logger.LogWarning($"Repository {repositoryId} not found");
                    return Json(new { success = false, message = $"Repository with ID {repositoryId} not found" });
                }

                // Check ownership
                if (repository.OwnerId != userId)
                {
                    _logger.LogWarning($"User {userId} attempted to scan repository {repositoryId} without permission");
                    return Json(new { success = false, message = "You don't have permission to scan this repository" });
                }

                var scan = new Scan
                {
                    RepositoryId = repositoryId,
                    Type = ScanType.SAST,
                    Status = ScanStatus.Pending,
                    ToolName = tool,
                    StartedAt = DateTime.UtcNow
                };

                _context.Scans.Add(scan);
                await _context.SaveChangesAsync();

                // Queue the scan job
                BackgroundJob.Enqueue<ScanJob>(job => job.ExecuteSastScanAsync(scan.Id, tool));

                _logger.LogInformation($"SAST scan queued for repository {repositoryId} using {tool}");

                return Json(new { success = true, scanId = scan.Id, message = $"{tool} scan started successfully. Check the scan status in a few moments." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting SAST scan for repository {repositoryId}");
                return Json(new { success = false, message = $"Error starting scan: {ex.Message}" });
            }
        }

        // POST: Scan/StartDAST
        [HttpPost]
        public async Task<IActionResult> StartDAST(int repositoryId)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var repository = await _context.Repositories.FindAsync(repositoryId);
            if (repository == null)
            {
                return NotFound();
            }

            // Check ownership
            if (repository.OwnerId != userId)
            {
                return Json(new { success = false, message = "You don't have permission to scan this repository" });
            }

            var scan = new Scan
            {
                RepositoryId = repositoryId,
                Type = ScanType.DAST,
                Status = ScanStatus.Pending,
                ToolName = "Nuclei",
                StartedAt = DateTime.UtcNow
            };

            _context.Scans.Add(scan);
            await _context.SaveChangesAsync();

            // Queue the scan job
            BackgroundJob.Enqueue<ScanJob>(job => job.ExecuteDastScanAsync(scan.Id));

            _logger.LogInformation($"DAST scan queued for repository {repositoryId}");

            return Json(new { success = true, scanId = scan.Id });
        }

        // POST: Scan/StartQuickDAST
        [HttpPost]
        public async Task<IActionResult> StartQuickDAST(string targetUrl)
        {
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                return Json(new { success = false, message = "Target URL is required" });
            }

            try
            {
                // Validate and normalize URL
                if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    targetUrl = "https://" + targetUrl;
                }

                Uri uri = new Uri(targetUrl);
                
                // Create a temporary repository entry for this URL
                var repository = new Repository
                {
                    Url = targetUrl,
                    LiveUrl = targetUrl,
                    Name = uri.Host,
                    Owner = "Quick Scan",
                    CreatedAt = DateTime.UtcNow,
                    OwnerId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int qUserId) ? qUserId : 0
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

                // Queue the scan job
                BackgroundJob.Enqueue<ScanJob>(job => job.ExecuteDastScanAsync(scan.Id));

                _logger.LogInformation($"Quick DAST scan queued for URL: {targetUrl}");

                return Json(new { success = true, scanId = scan.Id, message = $"DAST scan started for {targetUrl}" });
            }
            catch (UriFormatException)
            {
                return Json(new { success = false, message = "Invalid URL format. Please enter a valid URL (e.g., https://example.com)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting quick DAST scan for URL: {targetUrl}");
                return Json(new { success = false, message = $"An error occurred while starting the scan: {ex.Message}" });
            }
        }

        // GET: Scan/Status/5
        [HttpGet]
        public async Task<IActionResult> Status(int id)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var scan = await _context.Scans
                .Include(s => s.Repository)
                .Include(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (scan == null)
            {
                return NotFound();
            }

            // Check ownership
            if (scan.Repository.OwnerId != userId)
            {
                return Forbid();
            }

            return Json(new
            {
                status = scan.Status.ToString(),
                completedAt = scan.CompletedAt,
                vulnerabilityCount = scan.Vulnerabilities.Count,
                errorMessage = scan.ErrorMessage
            });
        }

        // GET: Scan/ExportPdf/5
        [HttpGet]
        public async Task<IActionResult> ExportPdf(int id)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var scan = await _context.Scans
                .Include(s => s.Repository)
                .Include(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (scan == null)
            {
                return NotFound();
            }

            // Check ownership
            if (scan.Repository.OwnerId != userId)
            {
                return Forbid();
            }

            var pdfBytes = _pdfReportService.GenerateScanReport(scan);
            var fileName = $"scan-report-{scan.Id}-{scan.Repository.Name}-{DateTime.Now:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        // GET: Scan/ExportCsv/5
        [HttpGet]
        public async Task<IActionResult> ExportCsv(int id)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var scan = await _context.Scans
                .Include(s => s.Repository)
                .Include(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (scan == null)
            {
                return NotFound();
            }

            // Check ownership
            if (scan.Repository.OwnerId != userId)
            {
                return Forbid();
            }

            var csvBytes = _reportService.GenerateCsvReport(scan);
            var fileName = $"scan-{scan.Id}-{scan.Repository.Name}-{DateTime.Now:yyyyMMdd}.csv";

            return File(csvBytes, "text/csv", fileName);
        }
    }
}
