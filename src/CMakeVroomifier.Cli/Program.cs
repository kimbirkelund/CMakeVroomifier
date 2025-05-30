using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((context, services) => { services.AddHostedService<HelloWorldService>(); })
               .Build();

await host.RunAsync();
