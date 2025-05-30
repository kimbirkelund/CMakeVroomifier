using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

public class HelloWorldService(ILogger<HelloWorldService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Markup("[green]Hello[/] [bold yellow]World![/]");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
