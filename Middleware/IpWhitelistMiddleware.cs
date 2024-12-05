using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Middleware
{
    public class IpWhitelistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HashSet<string> _allowedIps;

        public IpWhitelistMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _allowedIps = new HashSet<string>(
                configuration.GetSection("IpSecurity:AllowedIPs").Get<string[]>() ?? Array.Empty<string>()
            );
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            
            if (remoteIp == null || !_allowedIps.Contains(remoteIp))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync($"Dostęp zabroniony dla IP: {remoteIp}");
                return;
            }

            await _next(context);
        }
    }
} 