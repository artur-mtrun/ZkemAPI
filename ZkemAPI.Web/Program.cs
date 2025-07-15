using ZkemAPI.Core.Interfaces;
using ZkemAPI.SDK.Services;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Web;
using ZkemAPI.Web.Middleware;

// Debug NLog
Console.WriteLine("=== DEBUG NLOG ===");
Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
Console.WriteLine($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
Console.WriteLine($"nlog.config exists: {File.Exists("nlog.config")}");
Console.WriteLine($"nlog.config full path: {Path.GetFullPath("nlog.config")}");

// Lista wszystkich plików w katalogu
Console.WriteLine("Files in current directory:");
foreach (var file in Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*"))
{
    Console.WriteLine($"  {file}");
}

if (File.Exists("nlog.config"))
{
    Console.WriteLine($"nlog.config content: {File.ReadAllText("nlog.config").Substring(0, 200)}...");
}

// Early init of NLog to allow startup and exception logging, before host is built
// Konfiguracja NLog bezpośrednio w kodzie
var config = new NLog.Config.LoggingConfiguration();

// File target
var fileTarget = new NLog.Targets.FileTarget("logfile")
{
    FileName = @"C:\temp\zkemapi-${shortdate}.log",
    Layout = "${longdate} ${level:uppercase=true} ${logger} ${message} ${exception:format=tostring}"
};

var errorTarget = new NLog.Targets.FileTarget("errorfile")
{
    FileName = @"C:\temp\zkemapi-errors-${shortdate}.log",
    Layout = "${longdate} ${level:uppercase=true} ${logger} ${message} ${exception:format=tostring} ${stacktrace}"
};

var consoleTarget = new NLog.Targets.ConsoleTarget("console")
{
    Layout = "${time} [${level}] ${message} ${exception:format=tostring}"
};

// Rules
config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, fileTarget);
config.AddRule(NLog.LogLevel.Error, NLog.LogLevel.Fatal, errorTarget);
config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, consoleTarget);

LogManager.Configuration = config;
var logger = LogManager.GetCurrentClassLogger();
logger.Debug("Inicjalizacja aplikacji...");
logger.Info("TEST LOGOWANIA - to powinno się pojawić w pliku!");
logger.Error("TEST BŁĘDU - to powinno się pojawić w pliku błędów!");
Console.WriteLine("Logger initialized");
Console.WriteLine("Test logs written - check for files now!");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // NLog: Setup NLog for Dependency injection
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

// Wczytaj dozwolone hosty z pliku
var allowedHostsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "allowedHosts.json");
var allowedHosts = new List<string> { "localhost", "127.0.0.1" }; // domyślne wartości

if (File.Exists(allowedHostsPath))
{
    try
    {
        var allowedHostsConfig = JsonSerializer.Deserialize<AllowedHostsConfig>(
            File.ReadAllText(allowedHostsPath));
        allowedHosts = allowedHostsConfig?.AllowedHosts ?? allowedHosts;
        
        // Nadpisz konfigurację AllowedHosts
        builder.Configuration["AllowedHosts"] = string.Join(",", allowedHosts);
        Console.WriteLine($"Allowed Hosts: {string.Join(", ", allowedHosts)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd podczas wczytywania allowedHosts.json: {ex.Message}");
    }
}

// Dodaj middleware do sprawdzania hostów
builder.Services.Configure<HostFilteringOptions>(options =>
{
    options.AllowedHosts = allowedHosts;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "ZkemAPI", 
        Version = "v1",
        Description = "API do obsługi czytników ZKTeco"
    });
});

// Dodaj własne middleware do sprawdzania hostów
builder.Services.AddTransient<IStartupFilter, CustomHostFilteringStartupFilter>();

// Rejestracja konfiguracji DeviceSettings
builder.Services.Configure<ZkemAPI.SDK.Models.DeviceSettings>(
    builder.Configuration.GetSection("DeviceSettings"));

// Rejestracja serwisów do obsługi czytników
builder.Services.AddTransient<IZkemDevice>(provider => 
{
    var logger = provider.GetRequiredService<ILogger<ZkemDevice>>();
    return new ZkemDevice(logger);
});

builder.Services.AddSingleton<IDeviceConnectionManager>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<DeviceConnectionManager>>();
    var deviceFactory = new Func<IZkemDevice>(() => provider.GetRequiredService<IZkemDevice>());
    var deviceSettings = provider.GetRequiredService<IOptions<ZkemAPI.SDK.Models.DeviceSettings>>();
    return new DeviceConnectionManager(logger, deviceFactory, deviceSettings);
});

    var app = builder.Build();

    // Dodaj middleware do globalnego łapania wyjątków (na początku pipeline)
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // Dodaj middleware do filtrowania IP przed innymi middleware
    app.UseMiddleware<ClientIpFilterMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZkemAPI v1");
    c.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseAuthorization();
app.MapControllers();

    logger.Debug("Uruchamianie aplikacji...");
    app.Run();
}
catch (Exception exception)
{
    // NLog: catch setup errors
    logger.Error(exception, "Zatrzymano aplikację z powodu wyjątku");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
    LogManager.Shutdown();
}

// Klasy pomocnicze
public class AllowedHostsConfig
{
    public List<string> AllowedHosts { get; set; } = new();
}

public class CustomHostFilteringStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<CustomHostFilteringMiddleware>();
            next(app);
        };
    }
}

public class CustomHostFilteringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly HashSet<string> _allowedHosts;

    public CustomHostFilteringMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
        _allowedHosts = new HashSet<string>(
            configuration["AllowedHosts"]?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase
        );
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;

        if (_allowedHosts.Count > 0 && !_allowedHosts.Contains(host))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid host header");
            return;
        }

        await _next(context);
    }
}

public class ClientIpFilterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _allowedIps;
    private readonly ILogger<ClientIpFilterMiddleware> _logger;

    public ClientIpFilterMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ClientIpFilterMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        
        // Wczytaj dozwolone adresy IP z pliku
        var allowedIpsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "allowedIps.json");
        _allowedIps = new HashSet<string> { "127.0.0.1", "::1" }; // localhost IPv4 i IPv6

        if (File.Exists(allowedIpsPath))
        {
            try
            {
                var config = JsonSerializer.Deserialize<AllowedIpsConfig>(File.ReadAllText(allowedIpsPath));
                if (config?.AllowedIps != null)
                {
                    foreach (var ip in config.AllowedIps)
                    {
                        _allowedIps.Add(ip);
                    }
                }
                _logger.LogInformation($"Loaded allowed IPs: {string.Join(", ", _allowedIps)}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading allowedIps.json: {ex.Message}");
            }
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        _logger.LogInformation($"Request from IP: {clientIp}");

        if (clientIp != null && !_allowedIps.Contains(clientIp))
        {
            _logger.LogWarning($"Unauthorized access attempt from IP: {clientIp}");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Access denied. Your IP is not authorized.");
            return;
        }

        await _next(context);
    }
}

public class AllowedIpsConfig
{
    public List<string> AllowedIps { get; set; } = new();
}

