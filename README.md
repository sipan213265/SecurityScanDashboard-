# Security Scan Dashboard

ASP.NET Core MVC application for automated security testing of GitHub repositories using SAST and DAST tools.

## Features

- **Repository Management**: Add and manage GitHub repositories for scanning
- **SAST (Static Application Security Testing)**:
  - Semgrep pattern-based scanning
- **DAST (Dynamic Application Security Testing)**:
  - Nuclei vulnerability scanner
- **Dashboard**: Real-time overview of scans and vulnerabilities
- **Background Jobs**: Async scan execution with Hangfire
- **Vulnerability Tracking**: Detailed vulnerability reports with severity levels

## Technology Stack

- **Backend**: ASP.NET Core 8.0 MVC
- **Database**: PostgreSQL 16
- **ORM**: Entity Framework Core
- **Background Jobs**: Hangfire
- **Security Tools**: Semgrep, Nuclei
- **UI**: Bootstrap 5 + Bootstrap Icons

## Prerequisites

- .NET 8 SDK
- Docker Desktop (for PostgreSQL and Nuclei)
- Git CLI
- Semgrep CLI (install: `pip install semgrep`)

## Getting Started

### 1. Start Docker Services

```powershell
cd c:\Users\erasdfghjk\OneDrive\Masaüstü\SecrityScanDashboard
docker-compose up -d
```

This will start:
- PostgreSQL (port 5432)
- Nuclei (container)

### 2. Apply Database Migrations

```powershell
cd SecurityScanDashboard
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 3. Run the Application

```powershell
dotnet run
```

The application will be available at:
- Main app: https://localhost:5001
- Hangfire dashboard: https://localhost:5001/hangfire

### 4. Configure Security Tools

**Nuclei Setup:**
- Nuclei runs in Docker container and is ready to use
- Templates are automatically updated on container start
2. Update `appsettings.json` if you change the API key

## Configuration

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=securityscandb;Username=postgres;Password=postgres"
  },
  "SecurityTools": {
    "Nuclei": {
      "Enabled": true
    },
    "Semgrep": {
      "Enabled": true
    }
  },
  "ScanSettings": {
    "MaxConcurrentScans": 2,
    "ScanTimeoutMinutes": 30,
    "MaxRepoSizeMB": 500,
    "TempDirectory": "./temp"
  }
}
```

## Usage

1. **Add Repository**: Navigate to Repositories → Add Repository
2. **Start Scan**: Go to repository details and click SAST or DAST scan buttons
3. **View Results**: Check scan details and vulnerabilities
4. **Monitor Jobs**: Access Hangfire dashboard for job monitoring

## Project Structure

```
SecurityScanDashboard/
├── Controllers/          # MVC Controllers
├── Models/              # Domain models (Repository, Scan, Vulnerability)
├── Views/               # Razor views
├── Services/            # Tool integration services
├── Jobs/                # Hangfire background jobs
├── Data/                # EF Core DbContext
├── wwwroot/             # Static files
└── appsettings.json     # Configuration
```

## Database Schema

- **Repositories**: GitHub repository information
- **Scans**: Scan metadata and status
- **Vulnerabilities**: Detected security issues

## Future Enhancements

- [ ] Authentication & Authorization (ASP.NET Identity)
- [ ] Private repository support (GitHub OAuth)
- [ ] Real-time updates (SignalR)
- [ ] Export reports (PDF, CSV, JSON)
- [ ] Advanced filtering and search
- [ ] Email notifications
- [ ] Kubernetes deployment
- [ ] CI/CD pipeline integration

## License

MIT License
