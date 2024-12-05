using ZkemAPI.Core.Interfaces;
using ZkemAPI.SDK.Services;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.HostFiltering;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton<IZkemDevice, ZkemDevice>();

var app = builder.Build();

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

app.Run();

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

