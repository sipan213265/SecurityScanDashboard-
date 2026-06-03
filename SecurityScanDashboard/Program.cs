using Microsoft.EntityFrameworkCore;
using SecurityScanDashboard.Data;
using SecurityScanDashboard.Models;
using SecurityScanDashboard.Services;
using SecurityScanDashboard.Jobs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Http;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.Cookies;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Hangfire", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/security-scan-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Security Scan Dashboard application");

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add API Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Security Scan Dashboard API",
        Version = "v1",
        Description = "REST API for automated security scanning of GitHub repositories using SAST and DAST tools",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Security Scan Dashboard",
        }
    });

    // Enable XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure PostgreSQL with Entity Framework Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(60);
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
    });
    options.EnableSensitiveDataLogging(false);
    options.EnableDetailedErrors(false);
});

// Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// Configure Hangfire with PostgreSQL storage (separate database)
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
    {
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("HangfireConnection"));
    }, new Hangfire.PostgreSql.PostgreSqlStorageOptions
    {
        PrepareSchemaIfNecessary = true
    }));

// Add Hangfire server
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1; // Limit to 1 concurrent scan to reduce connection pressure
    options.ServerTimeout = TimeSpan.FromMinutes(30);
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
    options.HeartbeatInterval = TimeSpan.FromSeconds(30);
    options.ServerCheckInterval = TimeSpan.FromMinutes(1);
    options.CancellationCheckInterval = TimeSpan.FromSeconds(5);
});

// Add HttpClient for tool integrations
builder.Services.AddHttpClient();
builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
{
    options.HttpClientActions.Add(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5); // Nuclei scans are faster than ZAP
    });
});

// Register services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<ISemgrepService, SemgrepService>();
builder.Services.AddScoped<INucleiService, NucleiService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPdfReportService, PdfReportService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ScanJob>();
builder.Services.AddScoped<CleanupJob>();

// Add SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Note: Roles are managed in the school's database (public.roles table)
// No role seeding needed here

// Startup cleanup: Remove old temp files
var tempDirectory = builder.Configuration["ScanSettings:TempDirectory"] ?? "./temp";
if (Directory.Exists(tempDirectory))
{
    try
    {
        var oldDirs = Directory.GetDirectories(tempDirectory)
            .Where(d => Directory.GetCreationTime(d) < DateTime.Now.AddHours(-2));
        
        foreach (var dir in oldDirs)
        {
            try
            {
                // Force delete all files first
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { /* Ignore file-level errors */ }
                }
                
                Directory.Delete(dir, true);
                app.Logger.LogInformation($"Cleaned up old temp directory: {dir}");
            }
            catch (UnauthorizedAccessException)
            {
                // Silently ignore permission errors - will be cleaned next time
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning($"Failed to cleanup directory {dir}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to perform startup cleanup");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Security Scan Dashboard API v1");
    c.RoutePrefix = "api/docs"; // Access at /api/docs
    c.DocumentTitle = "Security Scan Dashboard API";
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

// Enable CORS
app.UseCors("AllowAll");

// Enable Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire");

// Schedule periodic cleanup job (every 2 hours) - AFTER app is built
RecurringJob.AddOrUpdate<CleanupJob>(
    "cleanup-temp-folders",
    job => job.CleanupOldTempFolders(),
    "0 */2 * * *"); // Every 2 hours

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<SecurityScanDashboard.Hubs.ScanHub>("/scanHub");

    // Apply pending EF migrations at startup (non-fatal if DB permissions are limited)
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityScanDashboard.Data.ApplicationDbContext>();
        db.Database.Migrate();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception migEx)
    {
        Log.Warning(migEx, "Could not apply migrations automatically — run SQL manually in Neon dashboard");
    }

    Log.Information("Security Scan Dashboard started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
