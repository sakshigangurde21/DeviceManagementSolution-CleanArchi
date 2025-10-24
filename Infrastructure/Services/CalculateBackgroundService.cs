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
        private readonly INotificationService _notificationService;
        private readonly IQueueService _queueService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CalculateBackgroundService> _logger;

        public CalculateBackgroundService(
            INotificationService notificationService,
            IQueueService queueService,
            IServiceScopeFactory scopeFactory,
            ILogger<CalculateBackgroundService> logger)
        {
            _notificationService = notificationService;
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
                        if (columnName.ToLower() != "temperature")
                        {
                            _logger.LogWarning($"Unknown column: {columnName}");
                            continue;
                        }

                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();

                        // Query the dummy table for average
                        double avgTemp = await dbContext.DeviceStatsDummy
                            .AverageAsync(d => d.Temperature, stoppingToken);

                        _logger.LogInformation($"Average Temperature: {avgTemp}");

                        await _notificationService.SendAverageToClients("Temperature", avgTemp);
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
