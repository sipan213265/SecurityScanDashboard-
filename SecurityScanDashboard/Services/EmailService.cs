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
        Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly ISettingsService _settings;
        private readonly IConfiguration _fallback;
        private readonly ILogger<EmailService> _logger;

        public EmailService(ISettingsService settings, IConfiguration fallback, ILogger<EmailService> logger)
        {
            _settings = settings;
            _fallback = fallback;
            _logger = logger;
        }

        // Read SMTP config: DB first, appsettings.json as fallback
        private async Task<(string host, int port, string user, string pass, string from, bool ssl, bool enabled)> LoadConfigAsync()
        {
            var all = await _settings.GetAllAsync();
            string G(string dbKey, string cfgKey, string def)
                => all.TryGetValue(dbKey, out var v) && !string.IsNullOrWhiteSpace(v) ? v
                   : (_fallback[cfgKey] ?? def);

            var host    = G("Email:SmtpHost",      "EmailSettings:SmtpHost",    "smtp.gmail.com");
            var portStr = G("Email:SmtpPort",      "EmailSettings:SmtpPort",    "587");
            var user    = G("Email:SmtpUsername",   "EmailSettings:SmtpUsername","");
            var pass    = G("Email:SmtpPassword",   "EmailSettings:SmtpPassword","");
            var from    = G("Email:FromEmail",      "EmailSettings:FromEmail",   "noreply@securityscan.com");
            var sslStr  = G("Email:EnableSsl",      "EmailSettings:EnableSsl",   "true");
            var enStr   = G("Email:SendOnComplete","EmailSettings:SendEmailOnScanComplete","true");

            return (host, int.TryParse(portStr, out var p) ? p : 587,
                    user, pass, from,
                    sslStr == "true", enStr == "true");
        }

        public async Task SendScanCompletedEmailAsync(Scan scan, string userEmail, string userName)
        {
            var (host, port, user, pass, from, ssl, enabled) = await LoadConfigAsync();

            if (!enabled || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(userEmail))
            {
                _logger.LogInformation("Email notification skipped — not configured or disabled.");
                return;
            }

            try
            {
                var criticalCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Critical);
                var highCount     = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.High);
                var mediumCount   = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Medium);
                var lowCount      = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Low);
                var totalCount    = scan.Vulnerabilities.Count;
                var statusColor   = scan.Status == ScanStatus.Completed ? "#28a745" : "#dc3545";
                var statusText    = scan.Status == ScanStatus.Completed ? "Completed Successfully" : "Failed";
                var fromName      = "Security Scan Dashboard";

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, from));
                message.To.Add(new MailboxAddress(userName, userEmail));
                message.Subject = $"Scan Completed: {scan.Repository.Name}";

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
        .vuln-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin: 15px 0; }}
        .vuln-card {{ padding: 15px; border-radius: 5px; text-align: center; color: white; }}
        .critical {{ background-color: #dc3545; }} .high {{ background-color: #ffc107; color:#333; }}
        .medium {{ background-color: #0dcaf0; color:#333; }} .low {{ background-color: #6c757d; }}
        .vuln-count {{ font-size: 24px; font-weight: bold; }} .vuln-label {{ font-size: 12px; text-transform: uppercase; }}
        .footer {{ text-align: center; padding: 20px; color: #6c757d; font-size: 12px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'><h1>&#128737;&#65039; Security Scan Report</h1></div>
        <div class='content'>
            <div class='status'><h2>{statusText}</h2></div>
            <div class='info-box'>
                <h3>Scan Details</h3>
                <p><strong>Repository:</strong> {scan.Repository.Name}</p>
                <p><strong>Scan Type:</strong> {scan.Type}</p>
                <p><strong>Tool:</strong> {scan.ToolName}</p>
                <p><strong>Started:</strong> {scan.StartedAt:yyyy-MM-dd HH:mm:ss}</p>
                {(scan.CompletedAt.HasValue ? $"<p><strong>Completed:</strong> {scan.CompletedAt.Value:yyyy-MM-dd HH:mm:ss}</p><p><strong>Duration:</strong> {(scan.CompletedAt.Value - scan.StartedAt).TotalMinutes:F1} min</p>" : "")}
            </div>
            {(scan.Status == ScanStatus.Completed ? $@"
            <div class='info-box'>
                <h3>Vulnerability Summary — Total: {totalCount}</h3>
                <div class='vuln-grid'>
                    <div class='vuln-card critical'><div class='vuln-count'>{criticalCount}</div><div class='vuln-label'>Critical</div></div>
                    <div class='vuln-card high'><div class='vuln-count'>{highCount}</div><div class='vuln-label'>High</div></div>
                    <div class='vuln-card medium'><div class='vuln-count'>{mediumCount}</div><div class='vuln-label'>Medium</div></div>
                    <div class='vuln-card low'><div class='vuln-count'>{lowCount}</div><div class='vuln-label'>Low</div></div>
                </div>
            </div>" : "")}
            {(scan.Status == ScanStatus.Failed && !string.IsNullOrEmpty(scan.ErrorMessage)
                ? $"<div class='info-box' style='border-left-color:#dc3545'><h3>Error</h3><p>{scan.ErrorMessage}</p></div>" : "")}
        </div>
        <div class='footer'><p>Security Scan Dashboard &mdash; {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p></div>
    </div>
</body>
</html>"
                };

                message.Body = bodyBuilder.ToMessageBody();
                await SendAsync(message, host, port, user, pass, ssl);
                _logger.LogInformation("Scan completion email sent to {Email}", userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send scan completion email to {Email}", userEmail);
            }
        }

        public async Task SendTestEmailAsync(string toEmail)
        {
            var (host, port, user, pass, from, ssl, _) = await LoadConfigAsync();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                throw new InvalidOperationException("SMTP username and password are not configured. Please save Email Settings first.");

            var fromName = "Security Scan Dashboard";
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, from));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = "Test Email — Security Scan Dashboard";
            message.Body = new BodyBuilder
            {
                HtmlBody = $@"
<html><body style='font-family:Arial,sans-serif;'>
  <div style='max-width:600px;margin:0 auto;padding:20px;'>
    <div style='background:#28a745;color:white;padding:20px;text-align:center;border-radius:5px;'>
      <h1>&#10003; Email Configuration Test</h1>
    </div>
    <div style='background:#f8f9fa;padding:20px;margin-top:16px;border:1px solid #dee2e6;'>
      <p>Your email configuration is working correctly!</p>
      <p>Security Scan Dashboard can now send you scan notifications.</p>
      <p style='color:#6c757d;font-size:12px;margin-top:20px;'>Sent: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
    </div>
  </div>
</body></html>"
            }.ToMessageBody();

            await SendAsync(message, host, port, user, pass, ssl);
        }

        private static async Task SendAsync(MimeMessage message, string host, int port, string user, string pass, bool ssl)
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, ssl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, pass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            var (host, port, user, pass, from, ssl, _) = await LoadConfigAsync();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                throw new InvalidOperationException("SMTP yapılandırması eksik. Lütfen Admin → Settings sayfasından email ayarlarını yapın.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Security Scan Dashboard", from));
            message.To.Add(new MailboxAddress(userName, toEmail));
            message.Subject = "Şifre Sıfırlama — Security Scan Dashboard";
            message.Body = new BodyBuilder
            {
                HtmlBody = $@"
<html><body style='font-family:Arial,sans-serif;'>
  <div style='max-width:600px;margin:0 auto;padding:20px;'>
    <div style='background:#dc3545;color:white;padding:20px;text-align:center;border-radius:5px;'>
      <h1>&#128274; Şifre Sıfırlama</h1>
    </div>
    <div style='background:#f8f9fa;padding:20px;margin-top:16px;border:1px solid #dee2e6;border-radius:5px;'>
      <p>Merhaba <strong>{userName}</strong>,</p>
      <p>Şifrenizi sıfırlamak için aşağıdaki butona tıklayın. Bu link <strong>1 saat</strong> geçerlidir.</p>
      <div style='text-align:center;margin:30px 0;'>
        <a href='{resetLink}' style='background:#dc3545;color:white;padding:14px 28px;text-decoration:none;border-radius:5px;font-size:16px;font-weight:bold;'>
          Şifremi Sıfırla
        </a>
      </div>
      <p>Bu butona tıklayamazsanız aşağıdaki linki kopyalayıp tarayıcınıza yapıştırın:</p>
      <p style='word-break:break-all;color:#6c757d;font-size:12px;'>{resetLink}</p>
      <hr>
      <p style='color:#6c757d;font-size:12px;'>Eğer şifre sıfırlama talebinde bulunmadıysanız bu emaili dikkate almayınız.</p>
    </div>
  </div>
</body></html>"
            }.ToMessageBody();

            await SendAsync(message, host, port, user, pass, ssl);
            _logger.LogInformation("Password reset email sent to {Email}", toEmail);
        }
    }
}

