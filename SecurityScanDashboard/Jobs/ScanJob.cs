using Hangfire;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;
using SecurityScanDashboard.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SecurityScanDashboard.Hubs;

namespace SecurityScanDashboard.Jobs
{
    public class ScanJob
    {
        private readonly ILogger<ScanJob> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ScanJob(ILogger<ScanJob> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task ExecuteSastScanAsync(int scanId, string tool)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var gitHubService = scope.ServiceProvider.GetRequiredService<IGitHubService>();

            var scan = await context.Scans
                .Include(s => s.Repository)
                .FirstOrDefaultAsync(s => s.Id == scanId);

            if (scan == null)
            {
                _logger.LogError($"Scan {scanId} not found");
                return;
            }

            string? repositoryPath = null;

            try
            {
                // Update scan status
                scan.Status = ScanStatus.Running;
                await context.SaveChangesAsync();

                // Notify clients via SignalR
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScanHub>>();
                await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Running", 10);

                // Clone repository
                repositoryPath = await gitHubService.CloneRepositoryAsync(scan.Repository.Url);
                await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Running", 30);

                List<Vulnerability> vulnerabilities = new();

                // Execute scan based on tool
                if (tool == "Semgrep")
                {
                    var semgrepService = scope.ServiceProvider.GetRequiredService<ISemgrepService>();
                    await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Running", 50);
                    vulnerabilities = await semgrepService.ScanAsync(repositoryPath);
                    await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Running", 80);
                }
                else
                {
                    throw new Exception($"Unknown SAST tool: {tool}");
                }

                // Save vulnerabilities
                foreach (var vuln in vulnerabilities)
                {
                    vuln.ScanId = scanId;
                    context.Vulnerabilities.Add(vuln);
                }

                // Update scan status
                scan.Status = ScanStatus.Completed;
                scan.CompletedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                // Calculate vulnerability counts
                var critical = vulnerabilities.Count(v => v.Severity == SeverityLevel.Critical);
                var high = vulnerabilities.Count(v => v.Severity == SeverityLevel.High);
                var medium = vulnerabilities.Count(v => v.Severity == SeverityLevel.Medium);
                var low = vulnerabilities.Count(v => v.Severity == SeverityLevel.Low);

                // Notify clients of completion
                await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Completed", 100);
                await hubContext.Clients.All.SendAsync("ReceiveScanCompleted", scanId, scan.Repository.Name, "Completed");
                await hubContext.Clients.All.SendAsync("ReceiveVulnerabilityUpdate", scanId, critical, high, medium, low);

                _logger.LogInformation($"SAST scan {scanId} completed successfully with {vulnerabilities.Count} vulnerabilities");

                // Send email notification
                try
                {
                    var repository = await context.Repositories
                        .Include(r => r.RepositoryOwner)
                        .FirstOrDefaultAsync(r => r.Id == scan.RepositoryId);
                    
                    if (repository?.RepositoryOwner != null)
                    {
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        await emailService.SendScanCompletedEmailAsync(scan, repository.RepositoryOwner.Email!, repository.RepositoryOwner.FullName);
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, $"Failed to send email notification for scan {scanId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"SAST scan {scanId} failed");

                scan.Status = ScanStatus.Failed;
                scan.ErrorMessage = ex.Message;
                scan.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                // Notify clients of failure
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScanHub>>();
                await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Failed", 0);
                await hubContext.Clients.All.SendAsync("ReceiveScanCompleted", scanId, scan.Repository.Name, "Failed");

                // Send email notification for failed scan
                try
                {
                    var repository = await context.Repositories
                        .Include(r => r.RepositoryOwner)
                        .FirstOrDefaultAsync(r => r.Id == scan.RepositoryId);
                    
                    if (repository?.RepositoryOwner != null)
                    {
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        await emailService.SendScanCompletedEmailAsync(scan, repository.RepositoryOwner.Email!, repository.RepositoryOwner.FullName);
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, $"Failed to send email notification for failed scan {scanId}");
                }
            }
            finally
            {
                // Cleanup
                if (repositoryPath != null)
                {
                    gitHubService.CleanupRepository(repositoryPath);
                }
            }
        }

        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
        public async Task ExecuteDastScanAsync(int scanId)
        {
            string targetUrl;
            IHubContext<ScanHub> hubContext;
            
            // Get scan details and update status in a SHORT-LIVED scope
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Database.SetCommandTimeout(TimeSpan.FromMinutes(1));
                hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScanHub>>();

                var scan = await context.Scans
                    .Include(s => s.Repository)
                    .FirstOrDefaultAsync(s => s.Id == scanId);

                if (scan == null)
                {
                    _logger.LogError($"Scan {scanId} not found");
                    return;
                }

                // Check if LiveUrl is configured
                if (string.IsNullOrWhiteSpace(scan.Repository.LiveUrl))
                {
                    throw new Exception("Live URL is not configured for this repository. Please add a Live URL to enable DAST scanning.");
                }

                targetUrl = scan.Repository.LiveUrl;

                // Update scan status
                scan.Status = ScanStatus.Running;
                await context.SaveChangesAsync();

                // Notify clients
                await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Running", 10);
            } // Scope closed - connection released BEFORE long-running scan

            List<Vulnerability> vulnerabilities;
            try
            {
                // Execute DAST scan WITHOUT holding database connection
                var nucleiService = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<INucleiService>();
                hubContext = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IHubContext<ScanHub>>();
                
                await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Running", 30);
                vulnerabilities = await nucleiService.ScanAsync(targetUrl);
                await hubContext.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Running", 70);

                // Save vulnerabilities in batches with NEW SCOPE for each batch
                const int batchSize = 50; // Reduced batch size for faster commits
                int savedCount = 0;
                for (int i = 0; i < vulnerabilities.Count; i += batchSize)
                {
                    var batch = vulnerabilities.Skip(i).Take(batchSize).ToList();
                    
                    // Create NEW SCOPE for each batch to avoid connection pool exhaustion
                    using (var batchScope = _serviceScopeFactory.CreateScope())
                    {
                        var batchContext = batchScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        batchContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
                        
                        foreach (var vuln in batch)
                        {
                            vuln.ScanId = scanId;
                            batchContext.Vulnerabilities.Add(vuln);
                        }
                        
                        // Retry logic for SaveChangesAsync
                        int retryCount = 0;
                        const int maxRetries = 3;
                        while (retryCount < maxRetries)
                        {
                            try
                            {
                                await batchContext.SaveChangesAsync();
                                savedCount += batch.Count;
                                _logger.LogInformation($"Saved batch of {batch.Count} vulnerabilities (total: {savedCount}/{vulnerabilities.Count})");
                                break;
                            }
                            catch (Exception ex) when (retryCount < maxRetries - 1)
                            {
                                retryCount++;
                                _logger.LogWarning($"Failed to save batch (attempt {retryCount}/{maxRetries}): {ex.Message}. Retrying in {retryCount * 2} seconds...");
                                await Task.Delay(retryCount * 2000);
                                
                                // Clear and re-add for retry
                                batchContext.ChangeTracker.Clear();
                                foreach (var vuln in batch)
                                {
                                    batchContext.Vulnerabilities.Add(vuln);
                                }
                            }
                        }
                    } // Scope disposed here - connection released immediately
                }

                // Update scan status in a NEW scope
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    context.Database.SetCommandTimeout(TimeSpan.FromMinutes(1));
                    var hubCtx = scope.ServiceProvider.GetRequiredService<IHubContext<ScanHub>>();
                    
                    var scan = await context.Scans.Include(s => s.Repository).FirstOrDefaultAsync(s => s.Id == scanId);
                    if (scan != null)
                    {
                        scan.Status = ScanStatus.Completed;
                        scan.CompletedAt = DateTime.UtcNow;
                        await context.SaveChangesAsync();

                        // Calculate vulnerability counts
                        var critical = vulnerabilities.Count(v => v.Severity == SeverityLevel.Critical);
                        var high = vulnerabilities.Count(v => v.Severity == SeverityLevel.High);
                        var medium = vulnerabilities.Count(v => v.Severity == SeverityLevel.Medium);
                        var low = vulnerabilities.Count(v => v.Severity == SeverityLevel.Low);

                        // Notify clients of completion
                        await hubCtx.Clients.All.SendAsync("ReceiveScanUpdate", scanId, "Completed", 100);
                        await hubCtx.Clients.All.SendAsync("ReceiveScanCompleted", scanId, scan.Repository.Name, "Completed");
                        await hubCtx.Clients.All.SendAsync("ReceiveVulnerabilityUpdate", scanId, critical, high, medium, low);

                        // Send email notification
                        try
                        {
                            var repository = await context.Repositories
                                .Include(r => r.RepositoryOwner)
                                .FirstOrDefaultAsync(r => r.Id == scan.RepositoryId);
                            
                            if (repository?.RepositoryOwner != null)
                            {
                                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                                await emailService.SendScanCompletedEmailAsync(scan, repository.RepositoryOwner.Email!, repository.RepositoryOwner.FullName);
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx, $"Failed to send email notification for DAST scan {scanId}");
                        }
                    }
                }

                _logger.LogInformation($"DAST scan {scanId} completed successfully with {vulnerabilities.Count} vulnerabilities");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DAST scan {scanId} failed");

                // Update failed status in a NEW scope
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    context.Database.SetCommandTimeout(TimeSpan.FromMinutes(1));
                    
                    var scan = await context.Scans.FindAsync(scanId);
                    if (scan != null)
                    {
                        scan.Status = ScanStatus.Failed;
                        scan.ErrorMessage = ex.Message;
                        scan.CompletedAt = DateTime.UtcNow;
                        await context.SaveChangesAsync();

                        // Send email notification for failed DAST scan
                        try
                        {
                            var repository = await context.Repositories
                                .Include(r => r.RepositoryOwner)
                                .FirstOrDefaultAsync(r => r.Id == scan.RepositoryId);
                            
                            if (repository?.RepositoryOwner != null)
                            {
                                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                                await emailService.SendScanCompletedEmailAsync(scan, repository.RepositoryOwner.Email!, repository.RepositoryOwner.FullName);
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx, $"Failed to send email notification for failed DAST scan {scanId}");
                        }
                    }
                }
            }
        }
    }
}
