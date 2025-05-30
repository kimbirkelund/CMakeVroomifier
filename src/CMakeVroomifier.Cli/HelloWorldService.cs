using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class HelloWorldService(ILogger<HelloWorldService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Hello, World!");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
