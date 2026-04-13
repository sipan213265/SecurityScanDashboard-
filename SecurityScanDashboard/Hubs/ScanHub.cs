using Microsoft.AspNetCore.SignalR;

namespace SecurityScanDashboard.Hubs
{
    public class ScanHub : Hub
    {
        private readonly ILogger<ScanHub> _logger;

        public ScanHub(ILogger<ScanHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        // Send scan status update to all clients
        public async Task SendScanUpdate(int scanId, string status, int progress)
        {
            await Clients.All.SendAsync("ReceiveScanUpdate", scanId, status, progress);
        }

        // Send scan completion notification
        public async Task SendScanCompleted(int scanId, string repositoryName, string status)
        {
            await Clients.All.SendAsync("ReceiveScanCompleted", scanId, repositoryName, status);
        }

        // Send vulnerability count update
        public async Task SendVulnerabilityUpdate(int scanId, int critical, int high, int medium, int low)
        {
            await Clients.All.SendAsync("ReceiveVulnerabilityUpdate", scanId, critical, high, medium, low);
        }

        // Send dashboard statistics update
        public async Task SendDashboardUpdate(object stats)
        {
            await Clients.All.SendAsync("ReceiveDashboardUpdate", stats);
        }

        // Notify specific user
        public async Task SendNotificationToUser(string userId, string message, string type)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", message, type);
        }
    }
}
