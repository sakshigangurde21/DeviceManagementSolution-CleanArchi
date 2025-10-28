using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class CalculateBackgroundService : BackgroundService
    {
        private readonly IQueueService _queueService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CalculateBackgroundService> _logger;

        public CalculateBackgroundService(
            IQueueService queueService,
            IServiceScopeFactory scopeFactory,
            ILogger<CalculateBackgroundService> logger)
        {
            _queueService = queueService;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background calculation service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_queueService.TryDequeue(out string columnName))
                    {
                        using var scope = _scopeFactory.CreateScope();

                        // Resolve scoped services within scope
                        var dbContext = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                        if (columnName.ToLower() != "temperature")
                        {
                            _logger.LogWarning($"Unknown column: {columnName}");
                            continue;
                        }

                        double avgTemp = await dbContext.DeviceStatsDummy
                            .AverageAsync(d => d.Temperature, stoppingToken);

                        _logger.LogInformation($"Average Temperature: {avgTemp}");

                        await notificationService.SendAverageToClients("Temperature", avgTemp);
                    }
                    else
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background calculation service");
                    await Task.Delay(2000, stoppingToken);
                }
            }

            _logger.LogInformation("Background calculation service stopped.");
        }
    }
}
