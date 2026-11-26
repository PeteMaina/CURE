using CURE.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCureInfrastructure(builder.Configuration);
builder.Services.AddHostedService<WorkerHeartbeat>();
var host = builder.Build();
await host.RunAsync();

public sealed class WorkerHeartbeat(ILogger<WorkerHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CURE worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
