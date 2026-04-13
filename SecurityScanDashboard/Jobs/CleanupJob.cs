namespace SecurityScanDashboard.Jobs
{
    public class CleanupJob
    {
        private readonly ILogger<CleanupJob> _logger;
        private readonly IConfiguration _configuration;

        public CleanupJob(ILogger<CleanupJob> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void CleanupOldTempFolders()
        {
            var tempDirectory = _configuration["ScanSettings:TempDirectory"] ?? "./temp";
            
            if (!Directory.Exists(tempDirectory))
            {
                return;
            }

            try
            {
                var oldDirs = Directory.GetDirectories(tempDirectory)
                    .Where(d => Directory.GetCreationTime(d) < DateTime.Now.AddHours(-2));

                foreach (var dir in oldDirs)
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        _logger.LogInformation($"Cleaned up old temp directory: {dir}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to cleanup directory: {dir}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform temp folder cleanup");
            }
        }
    }
}
