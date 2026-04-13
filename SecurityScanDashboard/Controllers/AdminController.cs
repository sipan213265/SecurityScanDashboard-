using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;
using System.Security.Claims;

namespace SecurityScanDashboard.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Admin
        public async Task<IActionResult> Index()
        {
            var stats = new
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalRepositories = await _context.Repositories.CountAsync(),
                TotalScans = await _context.Scans.CountAsync(),
                TotalVulnerabilities = await _context.Vulnerabilities.CountAsync(),
                ActiveScans = await _context.Scans.CountAsync(s => s.Status == ScanStatus.Running),
                FailedScans = await _context.Scans.CountAsync(s => s.Status == ScanStatus.Failed)
            };

            return View(stats);
        }

        // GET: Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ToListAsync();

            var usersWithStats = users.Select(user => new
            {
                user.Id,
                user.Username,
                user.Email,
                FullName = user.FullName,
                user.CreateDate,
                RepositoryCount = _context.Repositories.Count(r => r.OwnerId == user.Id),
                ScanCount = _context.Scans.Count(s => s.Repository.OwnerId == user.Id),
                Roles = user.UserRoles.Where(ur => ur.Role.IsActive).Select(ur => ur.Role.Name).ToList()
            }).ToList();

            return View(usersWithStats);
        }

        // GET: Admin/AllRepositories
        public async Task<IActionResult> AllRepositories()
        {
            var repositories = await _context.Repositories
                .Include(r => r.RepositoryOwner)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(repositories);
        }

        // GET: Admin/AllScans
        public async Task<IActionResult> AllScans()
        {
            var scans = await _context.Scans
                .Include(s => s.Repository)
                .ThenInclude(r => r.RepositoryOwner)
                .OrderByDescending(s => s.StartedAt)
                .ToListAsync();

            return View(scans);
        }

        // GET: Admin/Logs
        public IActionResult Logs(int page = 1, string level = "")
        {
            const int pageSize = 100;
            var logDirectory = "logs";
            var logFiles = new List<string>();

            if (Directory.Exists(logDirectory))
            {
                logFiles = Directory.GetFiles(logDirectory, "*.log")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToList();
            }

            ViewBag.LogFiles = logFiles.Select(f => Path.GetFileName(f)).ToList();
            ViewBag.CurrentPage = page;
            ViewBag.Level = level;

            return View();
        }

        // GET: Admin/GetLogContent
        public IActionResult GetLogContent(string fileName, int page = 1, string level = "")
        {
            const int pageSize = 100;
            var logPath = Path.Combine("logs", fileName);

            if (!System.IO.File.Exists(logPath))
            {
                return NotFound("Log file not found");
            }

            try
            {
                var allLines = System.IO.File.ReadAllLines(logPath).Reverse().ToList();
                
                // Filter by level if specified
                if (!string.IsNullOrEmpty(level))
                {
                    allLines = allLines.Where(line => line.Contains($"[{level}]", StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var totalPages = (int)Math.Ceiling(allLines.Count / (double)pageSize);
                var logs = allLines.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Json(new
                {
                    logs,
                    currentPage = page,
                    totalPages,
                    totalLogs = allLines.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading log file: {FileName}", fileName);
                return BadRequest("Error reading log file");
            }
        }

        // POST: Admin/AssignRole
        [HttpPost]
        public async Task<IActionResult> AssignRole(int userId, int roleId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            var role = await _context.Roles.FindAsync(roleId);
            if (role == null || !role.IsActive)
            {
                return Json(new { success = false, message = "Role not found or inactive" });
            }

            var existingUserRole = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

            if (existingUserRole != null)
            {
                return Json(new { success = false, message = $"User already has {role.Name} role" });
            }

            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                CreateDate = DateTime.UtcNow,
                OperationUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0")
            };

            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} assigned {Role} role to user {UserId}", 
                User.FindFirstValue(ClaimTypes.NameIdentifier), role.Name, userId);
            
            return Json(new { success = true, message = $"Successfully assigned {role.Name} role to {user.Username}" });
        }

        // POST: Admin/RemoveRole
        [HttpPost]
        public async Task<IActionResult> RemoveRole(int userId, int roleId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            var role = await _context.Roles.FindAsync(roleId);
            if (role == null)
            {
                return Json(new { success = false, message = "Role not found" });
            }

            var userRole = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

            if (userRole == null)
            {
                return Json(new { success = false, message = $"User does not have {role.Name} role" });
            }

            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} removed {Role} role from user {UserId}", 
                User.FindFirstValue(ClaimTypes.NameIdentifier), role.Name, userId);
            
            return Json(new { success = true, message = $"Successfully removed {role.Name} role from {user.Username}" });
        }

        // POST: Admin/DeleteRepository
        [HttpPost]
        public async Task<IActionResult> DeleteRepository(int id)
        {
            var repository = await _context.Repositories
                .Include(r => r.Scans)
                .ThenInclude(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (repository == null)
            {
                return Json(new { success = false, message = "Repository not found" });
            }

            try
            {
                // Delete all vulnerabilities first
                foreach (var scan in repository.Scans)
                {
                    _context.Vulnerabilities.RemoveRange(scan.Vulnerabilities);
                }

                // Delete all scans
                _context.Scans.RemoveRange(repository.Scans);

                // Delete repository
                _context.Repositories.Remove(repository);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin {AdminId} deleted repository {RepositoryId}", 
                    User.FindFirstValue(ClaimTypes.NameIdentifier), id);

                return Json(new { success = true, message = $"Repository '{repository.Name}' has been deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting repository {RepositoryId}", id);
                return Json(new { success = false, message = "Failed to delete repository: " + ex.Message });
            }
        }

        // GET: Admin/Settings
        public IActionResult Settings()
        {
            return View();
        }

        // POST: Admin/UpdateSettings
        [HttpPost]
        public async Task<IActionResult> UpdateSettings(string key, string value)
        {
            // This is a placeholder for settings management
            // In a real application, you would store settings in database or configuration
            _logger.LogInformation("Admin {AdminId} updated setting {Key} to {Value}", 
                User.FindFirstValue(ClaimTypes.NameIdentifier), key, value);

            return Json(new { success = true, message = "Settings updated successfully" });
        }

        // GET: Admin/SystemInfo
        public IActionResult SystemInfo()
        {
            var info = new
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                DotNetVersion = Environment.Version.ToString(),
                WorkingSet = Environment.WorkingSet / 1024 / 1024, // MB
                SystemDirectory = Environment.SystemDirectory,
                CurrentDirectory = Environment.CurrentDirectory,
                UpTime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            };

            return View(info);
        }
    }
}
