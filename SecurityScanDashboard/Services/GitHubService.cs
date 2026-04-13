namespace SecurityScanDashboard.Services
{
    public interface IGitHubService
    {
        Task<string> CloneRepositoryAsync(string repositoryUrl, CancellationToken cancellationToken = default);
        void CleanupRepository(string localPath);
    }

    public class GitHubService : IGitHubService
    {
        private readonly ILogger<GitHubService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _tempDirectory;

        public GitHubService(ILogger<GitHubService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _tempDirectory = configuration["ScanSettings:TempDirectory"] ?? "./temp";
            
            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }
        }

        public async Task<string> CloneRepositoryAsync(string repositoryUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                var repoName = Path.GetFileNameWithoutExtension(repositoryUrl.TrimEnd('/'));
                var localPath = Path.Combine(_tempDirectory, $"{repoName}_{Guid.NewGuid():N}");

                _logger.LogInformation($"Cloning repository {repositoryUrl} to {localPath}");

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone -c core.longpaths=true --depth 1 {repositoryUrl} \"{localPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    throw new Exception("Failed to start git process");
                }

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Git clone failed: {error}");
                }

                _logger.LogInformation($"Repository cloned successfully to {localPath}");
                return localPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to clone repository {repositoryUrl}");
                throw;
            }
        }

        public void CleanupRepository(string localPath)
        {
            try
            {
                if (Directory.Exists(localPath))
                {
                    Directory.Delete(localPath, true);
                    _logger.LogInformation($"Cleaned up repository at {localPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to cleanup repository at {localPath}");
            }
        }
    }
}
