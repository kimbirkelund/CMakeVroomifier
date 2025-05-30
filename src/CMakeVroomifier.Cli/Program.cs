using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureLogging(logging =>
               {
                   logging.ClearProviders(); // Remove all logging providers, including Console
                   // Optionally, add other providers here (e.g., logging.AddFile(), etc.)
               })
               .ConfigureServices((context, services) => { services.AddHostedService<HelloWorldService>(); })
               .Build();

await host.RunAsync();
