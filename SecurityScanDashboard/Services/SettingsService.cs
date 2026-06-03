using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;

namespace SecurityScanDashboard.Services
{
    public interface ISettingsService
    {
        Task<string> GetAsync(string key, string defaultValue = "");
        Task SetAsync(string key, string value);
        Task<Dictionary<string, string>> GetAllAsync();
        Task SetBulkAsync(Dictionary<string, string> settings);
    }

    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;

        public SettingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetAsync(string key, string defaultValue = "")
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value ?? defaultValue;
        }

        public async Task SetAsync(string key, string value)
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                _context.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }

        public async Task<Dictionary<string, string>> GetAllAsync()
        {
            return await _context.AppSettings
                .ToDictionaryAsync(s => s.Key, s => s.Value);
        }

        public async Task SetBulkAsync(Dictionary<string, string> settings)
        {
            var keys = settings.Keys.ToList();
            var existing = await _context.AppSettings
                .Where(s => keys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key);

            foreach (var kv in settings)
            {
                if (existing.TryGetValue(kv.Key, out var record))
                {
                    record.Value = kv.Value;
                    record.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.AppSettings.Add(new AppSetting
                    {
                        Key = kv.Key,
                        Value = kv.Value,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
