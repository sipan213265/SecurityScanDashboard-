using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;
using System.Security.Claims;

namespace SecurityScanDashboard.Controllers
{
    [Authorize]
    public class RepositoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RepositoryController> _logger;

        public RepositoryController(ApplicationDbContext context, ILogger<RepositoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Repository
        public async Task<IActionResult> Index()
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var repositories = await _context.Repositories
                .Where(r => r.OwnerId == userId)
                .Include(r => r.Scans)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(repositories);
        }

        // GET: Repository/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Repository/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Url,Name,Owner")] Repository repository)
        {
            // Set OwnerId before validation
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId);
            repository.OwnerId = currentUserId;
            ModelState.Remove("OwnerId"); // Remove OwnerId from validation since we set it manually
            
            if (ModelState.IsValid)
            {
                // Extract repository info from URL if not provided
                if (string.IsNullOrEmpty(repository.Name) || string.IsNullOrEmpty(repository.Owner))
                {
                    var (owner, name) = ExtractRepoInfoFromUrl(repository.Url);
                    repository.Owner = owner;
                    repository.Name = name;
                }

                repository.CreatedAt = DateTime.UtcNow;
                _context.Add(repository);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Repository created: {repository.Url}");
                TempData["SuccessMessage"] = $"Repository '{repository.Name}' has been added successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(repository);
        }

        // GET: Repository/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Repository ID is required.";
                return RedirectToAction(nameof(Index));
            }

            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var repository = await _context.Repositories
                .Include(r => r.Scans)
                    .ThenInclude(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (repository == null)
            {
                TempData["ErrorMessage"] = $"Repository with ID {id} not found. It may have been deleted.";
                return RedirectToAction(nameof(Index));
            }

            // Check ownership
            if (repository.OwnerId != userId)
            {
                TempData["ErrorMessage"] = "You don't have permission to view this repository.";
                return RedirectToAction(nameof(Index));
            }

            return View(repository);
        }

        // POST: Repository/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var repository = await _context.Repositories.FindAsync(id);
            
            if (repository == null)
            {
                TempData["ErrorMessage"] = "Repository not found.";
                return RedirectToAction(nameof(Index));
            }

            // Check ownership
            if (repository.OwnerId != userId)
            {
                TempData["ErrorMessage"] = "You don't have permission to delete this repository.";
                return RedirectToAction(nameof(Index));
            }

            var repoName = repository.Name;
            _context.Repositories.Remove(repository);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Repository deleted: {repository.Url}");
            TempData["SuccessMessage"] = $"Repository '{repoName}' has been deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private (string owner, string name) ExtractRepoInfoFromUrl(string url)
        {
            try
            {
                // Parse GitHub URL: https://github.com/owner/repo
                var uri = new Uri(url);
                var segments = uri.AbsolutePath.Trim('/').Split('/');
                
                if (segments.Length >= 2)
                {
                    return (segments[0], segments[1].Replace(".git", ""));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to parse repository URL: {url}", ex);
            }

            return ("Unknown", "Unknown");
        }
    }
}
