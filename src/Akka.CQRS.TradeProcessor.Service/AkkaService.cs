using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.CQRS.Infrastructure;
using Akka.CQRS.TradeProcessor.Actors;
using Microsoft.Extensions.Hosting;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Cluster.Sharding;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace Akka.CQRS.TradeProcessor.Service
{
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

            Cluster.Cluster.Get(_actorSystem).RegisterOnMemberUp(() =>
            {
                var sharding = ClusterSharding.Get(_actorSystem);

                var shardRegion = sharding.Start("orderBook", s => OrderBookActor.PropsFor(s), ClusterShardingSettings.Create(_actorSystem),
                    new StockShardMsgRouter());
            });

            // need to guarantee that host shuts down if ActorSystem shuts down
            _actorSystem.WhenTerminated.ContinueWith(tr =>
            {
                _lifetime.StopApplication();
            });

            // start Petabridge.Cmd (for external monitoring / supervision)
            var pbm = PetabridgeCmd.Get(_actorSystem);
            pbm.RegisterCommandPalette(ClusterCommands.Instance);
            pbm.RegisterCommandPalette(ClusterShardingCommands.Instance);
            pbm.RegisterCommandPalette(RemoteCommands.Instance);
            pbm.Start();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _actorSystem.Terminate();
        }
    }
}