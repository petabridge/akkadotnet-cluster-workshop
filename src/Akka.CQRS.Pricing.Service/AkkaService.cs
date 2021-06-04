using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Cluster.Tools.Singleton;
using Akka.Configuration;
using Akka.CQRS.Infrastructure;
using Akka.CQRS.Pricing.Actors;
using Akka.CQRS.Pricing.Cli;
using Akka.CQRS.Pricing.Commands;
using Microsoft.Extensions.Hosting;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Cluster.Sharding;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace Akka.CQRS.Pricing.Service
{
    /// <summary>
    /// IHostedService implementation for <see cref="ActorSystem"/>
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
            var config = File.ReadAllText("app.conf");
            var conf = ConfigurationFactory.ParseString(config);

            _actorSystem = ActorSystem.Create("AkkaTrader", AppBootstrap.BootstrapAkka(_provider,
                new AppBootstrapConfig(true, true), conf));

            var sharding = ClusterSharding.Get(_actorSystem);

            var shardRegion = sharding.Start("priceAggregator",
                s => Props.Create(() => new MatchAggregator(s)),
                ClusterShardingSettings.Create(_actorSystem),
                new StockShardMsgRouter());

            var priceInitiatorActor = _actorSystem.ActorOf(ClusterSingletonManager.Props(Props.Create(() => new PriceInitiatorActor(shardRegion)),
                ClusterSingletonManagerSettings.Create(_actorSystem)
                    .WithRole("pricing-engine")
                    .WithSingletonName("priceInitiator")), "priceInitiator");

            var clientHandler =
                _actorSystem.ActorOf(Props.Create(() => 
                    new ClientHandlerActor(shardRegion)), "subscriptions");

            // make ourselves available to ClusterClient at /user/subscriptions
            ClusterClientReceptionist.Get(_actorSystem).RegisterService(clientHandler);

            Cluster.Cluster.Get(_actorSystem).RegisterOnMemberUp(() =>
            {
                foreach (var ticker in AvailableTickerSymbols.Symbols)
                {
                    shardRegion.Tell(new Ping(ticker));
                }
            });

            // need to guarantee that host shuts down if ActorSystem shuts down
            _actorSystem.WhenTerminated.ContinueWith(tr =>
            {
                _lifetime.StopApplication();
            });

            // start Petabridge.Cmd (for external monitoring / supervision)
            var pbm = PetabridgeCmd.Get(_actorSystem);
            void RegisterPalette(CommandPaletteHandler h)
            {
                if (pbm.RegisterCommandPalette(h))
                {
                    Console.WriteLine("Petabridge.Cmd - Registered {0}", h.Palette.ModuleName);
                }
                else
                {
                    Console.WriteLine("Petabridge.Cmd - DID NOT REGISTER {0}", h.Palette.ModuleName);
                }
            }

            RegisterPalette(ClusterCommands.Instance);
            RegisterPalette(RemoteCommands.Instance);
            RegisterPalette(ClusterShardingCommands.Instance);
            RegisterPalette(new PriceCommands(shardRegion));
            pbm.Start();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _actorSystem.Terminate();
        }
    }
}