using System.Threading;
using System.Threading.Tasks;
using Akka.CQRS.Pricing.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace Akka.CQRS.Pricing.Web.Services
{
    /// <summary>
    /// Used to boostrap Akka.NET and SignalR inside ASP.NET MVC.
    /// </summary>
    public class StockBootstrap : IHostedService
    {
        private readonly IHubContext<StockHub> _hub;
        private AkkaService _akkaService;

        public StockBootstrap(IHubContext<StockHub> hub)
        {
            _hub = hub;
            _akkaService = new AkkaService();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _akkaService.StartActorSystem(new StockHubHelper(_hub));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _akkaService.Stop();
        }
    }
}