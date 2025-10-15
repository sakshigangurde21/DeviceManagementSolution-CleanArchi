namespace Devicemanagement.Middleware
{
    using Microsoft.AspNetCore.Http;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using System;
    using Infrastructure.Services;
    public class RequestCounterMiddleware : IMiddleware
    {
        private readonly ILogger<RequestCounterMiddleware> _logger;
        private readonly RequestCounterService _counterService;
        public RequestCounterMiddleware(
            ILogger<RequestCounterMiddleware> logger,
            RequestCounterService counterService)
        {
            _logger = logger;
            _counterService = counterService;
        }
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var method = context.Request.Method;
            var path = context.Request.Path.ToString()?.ToLower() ?? "";
            var key = $"{method} {path}";

            _counterService.RequestCounts.AddOrUpdate(key, 1, (_, v) => v + 1);

            _logger.LogInformation($"[{DateTime.Now}] {key} has been called {_counterService.RequestCounts[key]} times.");

            await next(context);
        }
    }
}
