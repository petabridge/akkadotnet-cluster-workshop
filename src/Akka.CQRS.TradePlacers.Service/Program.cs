using System.Threading.Tasks;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.CQRS.Infrastructure;
using Akka.CQRS.Infrastructure.Ops;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akka.CQRS.TradePlacers.Service
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    services.AddPhobosApm();
                    services.AddHostedService<AkkaService>();
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddConsole();
                })
                .UseConsoleLifetime()
                .Build();

            await host.RunAsync();
        }
    }
}
