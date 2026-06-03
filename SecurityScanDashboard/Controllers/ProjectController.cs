using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;
using System.Security.Claims;

namespace SecurityScanDashboard.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProjectController> _logger;

        public ProjectController(ApplicationDbContext context, ILogger<ProjectController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Project
        public async Task<IActionResult> Index()
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);

            var projects = await _context.Projects
                .Where(p => p.OwnerId == userId)
                .Include(p => p.Repositories)
                    .ThenInclude(r => r.Scans)
                        .ThenInclude(s => s.Vulnerabilities)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
        }

        // GET: Project/Details/5
        public async Task<IActionResult> Details(int id)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);

            var project = await _context.Projects
                .Include(p => p.Repositories)
                    .ThenInclude(r => r.Scans)
                        .ThenInclude(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(p => p.Id == id && (p.OwnerId == userId || User.IsInRole("Admin")));

            if (project == null) return NotFound();

            return View(project);
        }

        // GET: Project/Create
        public IActionResult Create() => View();

        // POST: Project/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description")] Project project)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            project.OwnerId = userId;
            project.CreatedAt = DateTime.UtcNow;
            ModelState.Remove("OwnerId");
            ModelState.Remove("Owner");

            if (ModelState.IsValid)
            {
                _context.Projects.Add(project);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"'{project.Name}' projesi oluşturuldu.";
                return RedirectToAction(nameof(Details), new { id = project.Id });
            }
            return View(project);
        }

        // GET: Project/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
            if (project == null) return NotFound();
            return View(project);
        }

        // POST: Project/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] Project project)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var existing = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
            if (existing == null) return NotFound();

            ModelState.Remove("OwnerId");
            ModelState.Remove("Owner");

            if (ModelState.IsValid)
            {
                existing.Name = project.Name;
                existing.Description = project.Description;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Proje güncellendi.";
                return RedirectToAction(nameof(Details), new { id });
            }
            return View(project);
        }

        // POST: Project/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);
            if (project == null) return NotFound();

            // Unlink repositories (don't delete them)
            var repos = await _context.Repositories.Where(r => r.ProjectId == id).ToListAsync();
            foreach (var r in repos) r.ProjectId = null;

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Proje silindi.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Project/AssignRepository
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRepository(int projectId, int repositoryId)
        {
            if (repositoryId == 0)
            {
                TempData["ErrorMessage"] = "Lütfen bir repository seçin.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == userId);
            var repo = await _context.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId && r.OwnerId == userId);
            if (project == null || repo == null) return NotFound();

            repo.ProjectId = projectId;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST: Project/AssignByUrl
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignByUrl(int projectId, string repoUrl)
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                TempData["ErrorMessage"] = "Lütfen bir GitHub URL'si girin.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            // Validate GitHub URL format
            var githubPattern = @"^https?://(www\.)?github\.com/[\w\-\.]+/[\w\-\.]+/?.*$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(repoUrl, githubPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                TempData["ErrorMessage"] = "Geçerli bir GitHub URL'si giriniz (örnek: https://github.com/owner/repo)";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == userId);
            if (project == null) return NotFound();

            // Normalize URL (remove trailing slash, .git suffix)
            repoUrl = repoUrl.TrimEnd('/').Replace(".git", "");

            // Check if repo already exists for this user
            var existing = await _context.Repositories
                .FirstOrDefaultAsync(r => r.OwnerId == userId && r.Url == repoUrl);

            if (existing != null)
            {
                if (existing.ProjectId == projectId)
                {
                    TempData["ErrorMessage"] = "Bu repository zaten bu projeye atanmış.";
                    return RedirectToAction(nameof(Details), new { id = projectId });
                }
                existing.ProjectId = projectId;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"'{existing.Name}' repository projeye atandı.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }

            // Create a new repository record from URL
            var uri = new Uri(repoUrl);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            var repoOwner = segments.Length >= 1 ? segments[0] : "Unknown";
            var repoName = segments.Length >= 2 ? segments[1] : "Unknown";

            var newRepo = new Repository
            {
                Url = repoUrl,
                Name = repoName,
                Owner = repoOwner,
                OwnerId = userId,
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Repositories.Add(newRepo);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"'{repoName}' repository oluşturuldu ve projeye atandı.";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        // POST: Project/RemoveRepository
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRepository(int projectId, int repositoryId)
        {
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
            var repo = await _context.Repositories.FirstOrDefaultAsync(r => r.Id == repositoryId && r.OwnerId == userId);
            if (repo == null) return NotFound();

            repo.ProjectId = null;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = projectId });
        }
    }
}
