using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.CQRS.Infrastructure;
using Akka.CQRS.Pricing.Web.Actors;
using Akka.CQRS.Pricing.Web.Hubs;
using Akka.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTracing;
using ServiceProvider = Akka.DependencyInjection.ServiceProvider;

namespace Akka.CQRS.Pricing.Web.Services
{
    /// <summary>
    /// Used to launch the <see cref="ActorSystem"/> and actors needed
    /// to communicate with the rest of the cluster.
    /// </summary>
    public sealed class AkkaService : IHostedService
    {
        private readonly IServiceProvider _provider;
        private readonly IHostApplicationLifetime _lifetime;
        private ActorSystem _actorSystem;

        public AkkaService(IServiceProvider provider, IHostApplicationLifetime lifetime)
        {
            _provider = provider;
            _lifetime = lifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var conf = ConfigurationFactory.ParseString(File.ReadAllText("app.conf"));

            _actorSystem = ActorSystem.Create("AkkaTrader", AppBootstrap.BootstrapAkka(_provider,
                new AppBootstrapConfig(false, false), conf));

            var tracing = _provider.GetRequiredService<ITracer>();

            var sp = ServiceProvider.For(_actorSystem);
            using(var createActorsSpan = tracing.BuildSpan("SpawnActors").StartActive()){
                var stockPublisherActor =
                _actorSystem.ActorOf(sp.Props<StockPublisherActor>(), "stockPublisher");

                var initialContactAddress = Environment.GetEnvironmentVariable("CLUSTER_SEEDS")?.Trim().Split(",")
                    .Select(x => Address.Parse(x)).ToList();

                if (initialContactAddress == null)
                {
                    _actorSystem.Log.Error("No initial cluster contacts found. Please be sure that the CLUSTER_SEEDS environment variable is populated with at least one address.");
                    return Task.FromException(new ConfigurationException(
                        "No initial cluster contacts found. Please be sure that the CLUSTER_SEEDS environment variable is populated with at least one address."));
                }

                var configurator = _actorSystem.ActorOf(
                    Props.Create(() => new StockEventConfiguratorActor(stockPublisherActor, initialContactAddress)),
                    "configurator");
            }


            // need to guarantee that host shuts down if ActorSystem shuts down
            _actorSystem.WhenTerminated.ContinueWith(tr =>
            {
                _lifetime.StopApplication();
            });

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _actorSystem.Terminate();
        }
    }
}
