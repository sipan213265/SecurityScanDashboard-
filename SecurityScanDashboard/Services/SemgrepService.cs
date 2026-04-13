using SecurityScanDashboard.Models;

namespace SecurityScanDashboard.Services
{
    public interface ISemgrepService
    {
        Task<List<Vulnerability>> ScanAsync(string repositoryPath, CancellationToken cancellationToken = default);
    }

    public class SemgrepService : ISemgrepService
    {
        private readonly ILogger<SemgrepService> _logger;

        public SemgrepService(ILogger<SemgrepService> logger)
        {
            _logger = logger;
        }

        public async Task<List<Vulnerability>> ScanAsync(string repositoryPath, CancellationToken cancellationToken = default)
        {
            var vulnerabilities = new List<Vulnerability>();

            try
            {
                _logger.LogInformation($"Starting Semgrep scan for {repositoryPath}");

                // Get the directory name from the full path for Docker volume mapping
                var repoFolderName = Path.GetFileName(repositoryPath);
                var dockerPath = $"/src/{repoFolderName}";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec securityscan_semgrep semgrep --config=auto --json {dockerPath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process == null)
                {
                    throw new Exception("Failed to start Semgrep process");
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                {
                    _logger.LogWarning($"Semgrep scan completed with warnings: {error}");
                }

                // Parse Semgrep JSON output
                if (!string.IsNullOrWhiteSpace(output))
                {
                    vulnerabilities = ParseSemgrepOutput(output);
                }

                _logger.LogInformation($"Semgrep scan completed. Found {vulnerabilities.Count} issues.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Semgrep scan failed");
                throw;
            }

            return vulnerabilities;
        }

        private List<Vulnerability> ParseSemgrepOutput(string jsonOutput)
        {
            var vulnerabilities = new List<Vulnerability>();

            try
            {
                _logger.LogInformation("Parsing Semgrep output");

                using var document = System.Text.Json.JsonDocument.Parse(jsonOutput);
                var root = document.RootElement;

                if (root.TryGetProperty("results", out var results))
                {
                    foreach (var result in results.EnumerateArray())
                    {
                        var vulnerability = new Vulnerability
                        {
                            Title = result.TryGetProperty("check_id", out var checkId) ? checkId.GetString() ?? "Unknown" : "Unknown",
                            Description = result.TryGetProperty("extra", out var extra) && extra.TryGetProperty("message", out var message) 
                                ? message.GetString() ?? "" : "",
                            Severity = MapSemgrepSeverity(result.TryGetProperty("extra", out var ex) && ex.TryGetProperty("severity", out var sev) 
                                ? sev.GetString() : "INFO"),
                            FilePath = result.TryGetProperty("path", out var path) ? path.GetString() ?? "" : "",
                            LineNumber = result.TryGetProperty("start", out var start) && start.TryGetProperty("line", out var line) 
                                ? line.GetInt32() : 0,
                            DetectedAt = DateTime.UtcNow
                        };

                        // Extract CWE if available
                        if (result.TryGetProperty("extra", out var extraData) && 
                            extraData.TryGetProperty("metadata", out var metadata) &&
                            metadata.TryGetProperty("cwe", out var cweArray))
                        {
                            var cweList = new List<string>();
                            foreach (var cwe in cweArray.EnumerateArray())
                            {
                                cweList.Add(cwe.GetString() ?? "");
                            }
                            vulnerability.CweId = string.Join(", ", cweList);
                        }

                        vulnerabilities.Add(vulnerability);
                    }
                }

                _logger.LogInformation($"Parsed {vulnerabilities.Count} vulnerabilities from Semgrep output");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Semgrep output");
            }

            return vulnerabilities;
        }

        private SeverityLevel MapSemgrepSeverity(string? severity)
        {
            return severity?.ToUpper() switch
            {
                "ERROR" => SeverityLevel.Critical,
                "WARNING" => SeverityLevel.High,
                "INFO" => SeverityLevel.Medium,
                _ => SeverityLevel.Low
            };
        }
    }
}
