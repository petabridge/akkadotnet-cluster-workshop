using System.Threading.Tasks;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.CQRS.Infrastructure;
using Akka.CQRS.Infrastructure.Ops;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akka.CQRS.TradePlacers.Service
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = WebHost.CreateDefaultBuilder(args)
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
                .Configure(app =>
                {
                    app.UseRouting();

                    // enable App.Metrics routes
                    app.UseMetricsAllMiddleware();
                    app.UseMetricsAllEndpoints();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
