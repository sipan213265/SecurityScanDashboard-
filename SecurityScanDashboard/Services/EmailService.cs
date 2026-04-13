using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using SecurityScanDashboard.Models;

namespace SecurityScanDashboard.Services
{
    public interface IEmailService
    {
        Task SendScanCompletedEmailAsync(Scan scan, string userEmail, string userName);
        Task SendTestEmailAsync(string toEmail);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly bool _enableSsl;
        private readonly bool _sendEmailEnabled;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _smtpHost = configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(configuration["EmailSettings:SmtpPort"] ?? "587");
            _smtpUsername = configuration["EmailSettings:SmtpUsername"] ?? "";
            _smtpPassword = configuration["EmailSettings:SmtpPassword"] ?? "";
            _fromEmail = configuration["EmailSettings:FromEmail"] ?? "noreply@securityscan.com";
            _fromName = configuration["EmailSettings:FromName"] ?? "Security Scan Dashboard";
            _enableSsl = bool.Parse(configuration["EmailSettings:EnableSsl"] ?? "true");
            _sendEmailEnabled = bool.Parse(configuration["EmailSettings:SendEmailOnScanComplete"] ?? "true");
        }

        public async Task SendScanCompletedEmailAsync(Scan scan, string userEmail, string userName)
        {
            if (!_sendEmailEnabled || string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(userEmail))
            {
                // Email not configured or disabled
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_fromName, _fromEmail));
                message.To.Add(new MailboxAddress(userName, userEmail));
                message.Subject = $"Scan Completed: {scan.Repository.Name}";

                var criticalCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Critical);
                var highCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.High);
                var mediumCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Medium);
                var lowCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Low);
                var totalCount = scan.Vulnerabilities.Count;

                var statusColor = scan.Status == ScanStatus.Completed ? "#28a745" : "#dc3545";
                var statusText = scan.Status == ScanStatus.Completed ? "Completed Successfully" : "Failed";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #343a40; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 20px; border: 1px solid #dee2e6; }}
        .status {{ background-color: {statusColor}; color: white; padding: 10px; border-radius: 5px; text-align: center; margin: 15px 0; }}
        .info-box {{ background-color: white; padding: 15px; margin: 10px 0; border-left: 4px solid #007bff; }}
        .vulnerability-summary {{ display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin: 15px 0; }}
        .vuln-card {{ padding: 15px; border-radius: 5px; text-align: center; color: white; }}
        .critical {{ background-color: #dc3545; }}
        .high {{ background-color: #ffc107; color: #333; }}
        .medium {{ background-color: #0dcaf0; color: #333; }}
        .low {{ background-color: #6c757d; }}
        .vuln-count {{ font-size: 24px; font-weight: bold; }}
        .vuln-label {{ font-size: 12px; text-transform: uppercase; }}
        .footer {{ text-align: center; padding: 20px; color: #6c757d; font-size: 12px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🛡️ Security Scan Report</h1>
        </div>
        <div class='content'>
            <div class='status'>
                <h2>{statusText}</h2>
            </div>
            
            <div class='info-box'>
                <h3>Scan Details</h3>
                <p><strong>Repository:</strong> {scan.Repository.Name}</p>
                <p><strong>Owner:</strong> {scan.Repository.Owner}</p>
                <p><strong>Scan Type:</strong> {scan.Type}</p>
                <p><strong>Tool:</strong> {scan.ToolName}</p>
                <p><strong>Started:</strong> {scan.StartedAt:yyyy-MM-dd HH:mm:ss}</p>
                {(scan.CompletedAt.HasValue ? $"<p><strong>Completed:</strong> {scan.CompletedAt.Value:yyyy-MM-dd HH:mm:ss}</p>" : "")}
                {(scan.CompletedAt.HasValue ? $"<p><strong>Duration:</strong> {(scan.CompletedAt.Value - scan.StartedAt).TotalMinutes:F1} minutes</p>" : "")}
            </div>

            {(scan.Status == ScanStatus.Completed ? $@"
            <div class='info-box'>
                <h3>Vulnerability Summary</h3>
                <p><strong>Total Vulnerabilities Found:</strong> {totalCount}</p>
                
                <div class='vulnerability-summary'>
                    <div class='vuln-card critical'>
                        <div class='vuln-count'>{criticalCount}</div>
                        <div class='vuln-label'>Critical</div>
                    </div>
                    <div class='vuln-card high'>
                        <div class='vuln-count'>{highCount}</div>
                        <div class='vuln-label'>High</div>
                    </div>
                    <div class='vuln-card medium'>
                        <div class='vuln-count'>{mediumCount}</div>
                        <div class='vuln-label'>Medium</div>
                    </div>
                    <div class='vuln-card low'>
                        <div class='vuln-count'>{lowCount}</div>
                        <div class='vuln-label'>Low</div>
                    </div>
                </div>
            </div>
            " : "")}

            {(scan.Status == ScanStatus.Failed && !string.IsNullOrEmpty(scan.ErrorMessage) ? $@"
            <div class='info-box' style='border-left-color: #dc3545;'>
                <h3>Error Details</h3>
                <p>{scan.ErrorMessage}</p>
            </div>
            " : "")}

            <div style='text-align: center;'>
                <a href='http://localhost:5297/Scan/Details/{scan.Id}' class='button'>View Full Report</a>
            </div>
        </div>
        <div class='footer'>
            <p>This is an automated message from Security Scan Dashboard.</p>
            <p>Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
        </div>
    </div>
</body>
</html>"
                };

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_smtpHost, _smtpPort, _enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                
                if (!string.IsNullOrEmpty(_smtpUsername))
                {
                    await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - email failure shouldn't break the scan process
                Console.WriteLine($"Failed to send email: {ex.Message}");
            }
        }

        public async Task SendTestEmailAsync(string toEmail)
        {
            if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(toEmail))
            {
                throw new InvalidOperationException("Email not configured or recipient email is empty");
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromEmail));
            message.To.Add(new MailboxAddress("Test User", toEmail));
            message.Subject = "Test Email from Security Scan Dashboard";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; }
        .container { max-width: 600px; margin: 0 auto; padding: 20px; }
        .header { background-color: #28a745; color: white; padding: 20px; text-align: center; border-radius: 5px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Email Configuration Test</h1>
        </div>
        <div style='background-color: #f8f9fa; padding: 20px; margin-top: 20px; border: 1px solid #dee2e6;'>
            <p>Your email configuration is working correctly!</p>
            <p>The Security Scan Dashboard can now send you notifications about scan completions.</p>
            <p style='margin-top: 20px; color: #6c757d; font-size: 12px;'>
                Sent on: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + @" UTC
            </p>
        </div>
    </div>
</body>
</html>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpHost, _smtpPort, _enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
            
            if (!string.IsNullOrEmpty(_smtpUsername))
            {
                await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
