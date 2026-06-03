using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;
using SecurityScanDashboard.Services;
using System.Security.Claims;

namespace SecurityScanDashboard.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly ISettingsService _settings;
        private readonly IEmailService _emailService;

        public AdminController(
            ApplicationDbContext context,
            ILogger<AdminController> logger,
            ISettingsService settings,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _settings = settings;
            _emailService = emailService;
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
                List<string> allLines;
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    allLines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Reverse().ToList();
                }
                
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
        public async Task<IActionResult> AssignRole(int userId, string role)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role && r.IsActive);
            if (roleEntity == null)
                return Json(new { success = false, message = $"Role '{role}' not found or inactive" });

            var existing = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleEntity.Id);
            if (existing != null)
                return Json(new { success = false, message = $"User already has {role} role" });

            _context.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleEntity.Id,
                CreateDate = DateTime.UtcNow,
                OperationUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0")
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin {AdminId} assigned {Role} to user {UserId}",
                User.FindFirstValue(ClaimTypes.NameIdentifier), role, userId);
            return Json(new { success = true, message = $"'{role}' rolü {user.Username} kullanıcısına atandı" });
        }

        // POST: Admin/RemoveRole
        [HttpPost]
        public async Task<IActionResult> RemoveRole(int userId, string role)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role);
            if (roleEntity == null)
                return Json(new { success = false, message = $"Role '{role}' not found" });

            var userRole = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleEntity.Id);
            if (userRole == null)
                return Json(new { success = false, message = $"User does not have {role} role" });

            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin {AdminId} removed {Role} from user {UserId}",
                User.FindFirstValue(ClaimTypes.NameIdentifier), role, userId);
            return Json(new { success = true, message = $"'{role}' rolü {user.Username} kullanıcısından kaldırıldı" });
        }

        // POST: Admin/DeleteUser  (sadece Admin rolü — controller seviyesinde korunuyor)
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (id == currentUserId)
                return Json(new { success = false, message = "Kendi hesabınızı silemezsiniz." });

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });

            var username = user.Username;
            try
            {
                // 1) ValidatedBy referanslarını temizle
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE belek_appsec.\"Vulnerabilities\" SET \"ValidatedBy\" = NULL WHERE \"ValidatedBy\" = {id}");

                // 2) repo → scan → vulnerability (EF)
                var repos = await _context.Repositories
                    .Include(r => r.Scans).ThenInclude(s => s.Vulnerabilities)
                    .Where(r => r.OwnerId == id).ToListAsync();
                foreach (var repo in repos)
                {
                    foreach (var scan in repo.Scans)
                        _context.Vulnerabilities.RemoveRange(scan.Vulnerabilities);
                    _context.Scans.RemoveRange(repo.Scans);
                }
                _context.Repositories.RemoveRange(repos);
                await _context.SaveChangesAsync();

                // 3) Projects (FK: OwnerId → users.id, Restrict)
                var projects = await _context.Projects.Where(p => p.OwnerId == id).ToListAsync();
                _context.Projects.RemoveRange(projects);
                await _context.SaveChangesAsync();

                // 4) public.user_roles
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM public.user_roles WHERE user_id = {id}");

                // 5) public.users
                int deleted = await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM public.users WHERE id = {id}");

                if (deleted == 0)
                    return Json(new { success = false, message = "Kullanıcı silinemedi. Veritabanı yetki ayarlarını kontrol edin (RLS politikası)." });

                _logger.LogInformation("Admin {AdminId} kullanıcıyı sildi: {UserId} ({Username})", currentUserId, id, username);
                return Json(new { success = true, message = $"{username} kullanıcısı silindi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteUser hatası: userId={UserId}", id);
                return Json(new { success = false, message = "Silme hatası: " + ex.Message });
            }
        }

        // POST: Admin/DeleteScan
        [HttpPost]
        public async Task<IActionResult> DeleteScan(int id)
        {
            var scan = await _context.Scans
                .Include(s => s.Vulnerabilities)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (scan == null)
                return Json(new { success = false, message = "Tarama bulunamadı." });

            try
            {
                _context.Vulnerabilities.RemoveRange(scan.Vulnerabilities);
                _context.Scans.Remove(scan);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Admin {AdminId} deleted scan {ScanId}",
                    User.FindFirstValue(ClaimTypes.NameIdentifier), id);
                return Json(new { success = true, message = $"Tarama #{id} silindi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting scan {ScanId}", id);
                return Json(new { success = false, message = "Silme hatası: " + ex.Message });
            }
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
        public async Task<IActionResult> Settings()
        {
            var allSettings = await _settings.GetAllAsync();

            // Provide defaults for keys that haven't been saved yet
            var defaults = new Dictionary<string, string>
            {
                ["Email:SmtpHost"]              = "smtp.gmail.com",
                ["Email:SmtpPort"]              = "587",
                ["Email:SmtpUsername"]          = "",
                ["Email:SmtpPassword"]          = "",
                ["Email:FromEmail"]             = "noreply@securityscan.com",
                ["Email:EnableSsl"]             = "true",
                ["Email:SendOnComplete"]        = "true",
                ["Scan:MaxConcurrent"]          = "2",
                ["Scan:TimeoutMinutes"]         = "30",
                ["Scan:AutoSchedule"]           = "none",
                ["App:LogRetentionDays"]        = "30",
                ["App:ItemsPerPage"]            = "10",
            };

            foreach (var kv in defaults)
                allSettings.TryAdd(kv.Key, kv.Value);

            return View(allSettings);
        }

        // POST: Admin/UpdateSettings
        [HttpPost]
        public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, string> settings)
        {
            if (settings == null || settings.Count == 0)
                return Json(new { success = false, message = "No settings provided" });

            try
            {
                await _settings.SetBulkAsync(settings);
                _logger.LogInformation("Admin {AdminId} updated settings: {Keys}",
                    User.FindFirstValue(ClaimTypes.NameIdentifier), string.Join(", ", settings.Keys));
                return Json(new { success = true, message = "Settings saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                return Json(new { success = false, message = "Failed to save settings: " + ex.Message });
            }
        }

        // POST: Admin/SendTestEmail
        [HttpPost]
        public async Task<IActionResult> SendTestEmail([FromBody] SendTestEmailRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
                return Json(new { success = false, message = "Email address is required" });
            try
            {
                await _emailService.SendTestEmailAsync(request.Email);
                return Json(new { success = true, message = $"Test email sent to {request.Email}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test email failed");
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class SendTestEmailRequest { public string Email { get; set; } = ""; }

        // GET: Admin/SystemInfo
        public IActionResult SystemInfo()
        {
            ViewBag.MachineName = Environment.MachineName;
            ViewBag.OSVersion = Environment.OSVersion.ToString();
            ViewBag.ProcessorCount = Environment.ProcessorCount;
            ViewBag.DotNetVersion = Environment.Version.ToString();
            ViewBag.WorkingSet = Environment.WorkingSet / 1024 / 1024; // MB
            ViewBag.GCMemory = GC.GetTotalMemory(false) / 1024 / 1024;
            ViewBag.SystemDirectory = Environment.SystemDirectory;
            ViewBag.CurrentDirectory = Environment.CurrentDirectory;
            ViewBag.UpTime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            return View();
        }
    }
}
