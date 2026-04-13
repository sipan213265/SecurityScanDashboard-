using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;
using System.Security.Claims;

namespace SecurityScanDashboard.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId);
        
        // Dashboard statistics (filtered by user)
        var totalRepos = await _context.Repositories.CountAsync(r => r.OwnerId == userId);
        var totalScans = await _context.Scans.CountAsync(s => s.Repository.OwnerId == userId);
        var runningScans = await _context.Scans.CountAsync(s => s.Status == ScanStatus.Running && s.Repository.OwnerId == userId);
        var totalVulnerabilities = await _context.Vulnerabilities.CountAsync(v => v.Scan.Repository.OwnerId == userId);

        // Running scans
        var runningScansDetails = await _context.Scans
            .Include(s => s.Repository)
            .Where(s => s.Status == ScanStatus.Running && s.Repository.OwnerId == userId)
            .OrderBy(s => s.StartedAt)
            .ToListAsync();

        // Recent scans
        var recentScans = await _context.Scans
            .Include(s => s.Repository)
            .Include(s => s.Vulnerabilities)
            .Where(s => s.Repository.OwnerId == userId)
            .OrderByDescending(s => s.StartedAt)
            .Take(10)
            .ToListAsync();

        // Vulnerabilities by severity (filtered by user)
        var criticalCount = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Critical && v.Scan.Repository.OwnerId == userId);
        var highCount = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.High && v.Scan.Repository.OwnerId == userId);
        var mediumCount = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Medium && v.Scan.Repository.OwnerId == userId);
        var lowCount = await _context.Vulnerabilities.CountAsync(v => v.Severity == SeverityLevel.Low && v.Scan.Repository.OwnerId == userId);

        ViewBag.TotalRepos = totalRepos;
        ViewBag.TotalScans = totalScans;
        ViewBag.RunningScans = runningScans;
        ViewBag.TotalVulnerabilities = totalVulnerabilities;
        ViewBag.CriticalCount = criticalCount;
        ViewBag.HighCount = highCount;
        ViewBag.MediumCount = mediumCount;
        ViewBag.LowCount = lowCount;
        ViewBag.RunningScansDetails = runningScansDetails;
        ViewBag.RecentScans = recentScans;

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
