using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;

namespace SecurityScanDashboard.Services
{
    public interface IAuthenticationService
    {
        Task<(bool Success, string Message, User? User)> RegisterAsync(string username, string email, string password, string firstName, string lastName);
        Task<(bool Success, string Message, User? User)> LoginAsync(HttpContext httpContext, string username, string password, bool rememberMe);
        Task LogoutAsync(HttpContext httpContext);
        Task<User?> GetCurrentUserAsync(ClaimsPrincipal claimsPrincipal);
        Task<bool> IsInRoleAsync(int userId, string roleName);
        string HashPassword(string password);
        bool VerifyPassword(string password, string passwordHash);
        Task<(bool Success, string Message)> GeneratePasswordResetTokenAsync(string email);
        Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword);
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(ApplicationDbContext context, ILogger<AuthenticationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Success, string Message, User? User)> RegisterAsync(
            string username, string email, string password, string firstName, string lastName)
        {
            try
            {
                // Check if username exists
                if (await _context.Users.AnyAsync(u => u.Username == username))
                {
                    return (false, "Bu kullanıcı adı zaten kullanılıyor.", null);
                }

                // Check if email exists
                if (await _context.Users.AnyAsync(u => u.Email == email))
                {
                    return (false, "Bu e-posta adresi zaten kullanılıyor.", null);
                }

                // Use raw SQL INSERT so created_by is set via subquery (required by RLS policy)
                // ExecuteSqlRawAsync is used instead of SqlQueryRaw to avoid non-composable SQL error
                await _context.Database.ExecuteSqlRawAsync(@"
                        INSERT INTO public.users
                            (username, email, password_hash, first_name, last_name,
                             user_type, is_active, language_code, create_date, update_date, created_by)
                        VALUES
                            ({0}, {1}, {2}, {3}, {4},
                             'Student', true, 'tr', now(), now(),
                             (SELECT user_id FROM public.user_schemas WHERE schema_name = current_user))",
                        username, email, HashPassword(password),
                        firstName ?? "", lastName ?? "");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                    return (false, "Kayıt oluşturuldu ancak kullanıcı bilgisi alınamadı.", null);

                _logger.LogInformation("New user registered: {Username} ({Email})", username, email);

                return (true, "Kayıt başarılı!", user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return (false, "Kayıt sırasında bir hata oluştu.", null);
            }
        }

        public async Task<(bool Success, string Message, User? User)> LoginAsync(
            HttpContext httpContext, string username, string password, bool rememberMe)
        {
            try
            {
                // Find user by username or email
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Username == username || u.Email == username);

                if (user == null)
                {
                    return (false, "Kullanıcı adı veya şifre hatalı.", null);
                }

                // Verify password
                if (!VerifyPassword(password, user.PasswordHash))
                {
                    return (false, "Kullanıcı adı veya şifre hatalı.", null);
                }

                // Create claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("FullName", user.FullName)
                };

                // Load roles via direct ADO.NET to bypass EF query engine and any RLS issues
                _logger.LogInformation("Loading roles for user {Username} (id={Id})", user.Username, user.Id);

                var roleNames = new List<string>();
                var conn = _context.Database.GetDbConnection();
                var wasOpen = conn.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"SELECT r.name FROM public.user_roles ur
                                        JOIN public.roles r ON r.id = ur.role_id
                                        WHERE ur.user_id = @uid";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "uid";
                    p.Value = user.Id;
                    cmd.Parameters.Add(p);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        roleNames.Add(reader.GetString(0));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load roles for {Username}", user.Username);
                }
                finally
                {
                    if (!wasOpen) await conn.CloseAsync();
                }

                _logger.LogInformation("Roles for {Username}: [{Roles}]", user.Username, string.Join(", ", roleNames));

                // Fallback: if user_roles RLS blocks the query, use UserType as role source
                if (roleNames.Count == 0 && !string.IsNullOrEmpty(user.UserType))
                {
                    _logger.LogWarning("user_roles returned empty for {Username}, falling back to UserType='{UserType}'", user.Username, user.UserType);
                    roleNames.Add(user.UserType);
                }

                foreach (var roleName in roleNames)
                {
                    // Normalize: first letter upper, rest lower (e.g. "admin" → "Admin")
                    var normalized = char.ToUpper(roleName[0]) + roleName.Substring(1).ToLower();
                    claims.Add(new Claim(ClaimTypes.Role, normalized));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
                };

                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("User logged in: {Username}", username);

                return (true, "Giriş başarılı!", user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return (false, "Giriş sırasında bir hata oluştu.", null);
            }
        }

        public async Task LogoutAsync(HttpContext httpContext)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("User logged out");
        }

        public async Task<User?> GetCurrentUserAsync(ClaimsPrincipal claimsPrincipal)
        {
            var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return null;
            }

            return await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<bool> IsInRoleAsync(int userId, string roleName)
        {
            return await _context.UserRoles
                .Include(ur => ur.Role)
                .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == roleName && ur.Role.IsActive);
        }

        public string HashPassword(string password)
        {
            // Use SHA256 for hashing (matching school's system)
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            var hashedInput = HashPassword(password);
            return hashedInput == passwordHash;
        }

        public async Task<(bool Success, string Message)> GeneratePasswordResetTokenAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                // Don't reveal if email exists
                return (true, "Eğer bu email kayıtlıysa, şifre sıfırlama linki gönderildi.");

            // Generate a secure random token
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", "");

            // Store in AppSettings: key = "pwd_reset_{token}", value = "{userId}|{expires}"
            var expires = DateTime.UtcNow.AddHours(1).ToString("o");
            var settingKey = $"pwd_reset_{token}";
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
            if (setting == null)
            {
                _context.AppSettings.Add(new AppSetting
                {
                    Key = settingKey,
                    Value = $"{user.Id}|{expires}",
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = $"{user.Id}|{expires}";
                setting.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset token generated for user {Username}", user.Username);
            return (true, token);
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword)
        {
            var settingKey = $"pwd_reset_{token}";
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
            if (setting == null)
                return (false, "Geçersiz veya süresi dolmuş şifre sıfırlama linki.");

            var parts = setting.Value.Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int userId))
                return (false, "Geçersiz token formatı.");

            if (!DateTime.TryParse(parts[1], out var expires) || expires < DateTime.UtcNow)
            {
                _context.AppSettings.Remove(setting);
                await _context.SaveChangesAsync();
                return (false, "Şifre sıfırlama linkinin süresi dolmuş. Lütfen yeniden talep edin.");
            }

            var newHash = HashPassword(newPassword);
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE public.users SET password_hash = {0}, update_date = now() WHERE id = {1}",
                newHash, userId);

            // Remove the token
            _context.AppSettings.Remove(setting);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset completed for user id={UserId}", userId);
            return (true, "Şifreniz başarıyla sıfırlandı. Yeni şifrenizle giriş yapabilirsiniz.");
        }
    }
}
