using SecurityScanDashboard.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecurityScanDashboard.Services
{
    public interface INucleiService
    {
        Task<List<Vulnerability>> ScanAsync(string targetUrl, CancellationToken cancellationToken = default);
    }

    public class NucleiService : INucleiService
    {
        private readonly ILogger<NucleiService> _logger;
        private readonly IConfiguration _configuration;

        public NucleiService(
            ILogger<NucleiService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<Vulnerability>> ScanAsync(string targetUrl, CancellationToken cancellationToken = default)
        {
            var vulnerabilities = new List<Vulnerability>();

            try
            {
                _logger.LogInformation($"Starting Nuclei scan for {targetUrl}");

                // Generate unique output filename
                var outputFileName = $"nuclei-{Guid.NewGuid()}.jsonl";
                
                // Run Nuclei scan via Docker
                var startInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec securityscan_nuclei nuclei -u {targetUrl} -t /root/nuclei-templates/http -jsonl -o /output/{outputFileName} -severity info,low,medium,high,critical -rl 50 -c 10 -silent",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new Exception("Failed to start Nuclei process");
                }

                // Read output for logging
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning($"Nuclei process exited with code {process.ExitCode}. Error: {error}");
                }

                _logger.LogInformation("Nuclei scan completed, parsing results...");

                // Parse results from mounted volume path
                var tempDirectory = _configuration["ScanSettings:TempDirectory"] ?? "./temp";
                var resultPath = Path.Combine(tempDirectory, outputFileName);
                
                if (File.Exists(resultPath))
                {
                    var jsonLines = await File.ReadAllLinesAsync(resultPath, cancellationToken);
                    
                    foreach (var line in jsonLines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        try
                        {
                            var result = JsonSerializer.Deserialize<NucleiResult>(line);
                            if (result != null)
                            {
                                vulnerabilities.Add(new Vulnerability
                                {
                                    Title = result.Info?.Name ?? "Unknown",
                                    Description = result.Info?.Description ?? string.Empty,
                                    Severity = MapSeverity(result.Info?.Severity),
                                    CweId = string.Join(",", result.Info?.Classification?.CweId ?? new List<string>()),
                                    CveId = string.Join(",", result.Info?.Classification?.CveId ?? new List<string>()),
                                    DetectedAt = DateTime.UtcNow
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to parse Nuclei result line: {ex.Message}");
                        }
                    }

                    // Cleanup
                    File.Delete(resultPath);
                }

                _logger.LogInformation($"Nuclei scan completed. Found {vulnerabilities.Count} vulnerabilities.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during Nuclei scan for {targetUrl}");
                throw;
            }

            return vulnerabilities;
        }

        private SeverityLevel MapSeverity(string? severity)
        {
            return severity?.ToLower() switch
            {
                "critical" => SeverityLevel.Critical,
                "high" => SeverityLevel.High,
                "medium" => SeverityLevel.Medium,
                "low" => SeverityLevel.Low,
                _ => SeverityLevel.Low
            };
        }

        // Nuclei JSON models
        private class NucleiResult
        {
            [JsonPropertyName("template-id")]
            public string? TemplateId { get; set; }
            
            [JsonPropertyName("info")]
            public NucleiInfo? Info { get; set; }
            
            [JsonPropertyName("type")]
            public string? Type { get; set; }
            
            [JsonPropertyName("host")]
            public string? Host { get; set; }
            
            [JsonPropertyName("matched-at")]
            public string? MatchedAt { get; set; }
        }

        private class NucleiInfo
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            
            [JsonPropertyName("description")]
            public string? Description { get; set; }
            
            [JsonPropertyName("severity")]
            public string? Severity { get; set; }
            
            [JsonPropertyName("classification")]
            public NucleiClassification? Classification { get; set; }
        }

        private class NucleiClassification
        {
            [JsonPropertyName("cwe-id")]
            public List<string>? CweId { get; set; }
            
            [JsonPropertyName("cve-id")]
            public List<string>? CveId { get; set; }
        }
    }
}
