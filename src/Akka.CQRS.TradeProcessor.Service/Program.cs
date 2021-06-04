using System.Threading.Tasks;
using Akka.CQRS.Infrastructure;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akka.CQRS.TradeProcessor.Service
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
