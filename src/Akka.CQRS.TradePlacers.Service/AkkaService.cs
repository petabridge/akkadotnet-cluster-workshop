// -----------------------------------------------------------------------
// <copyright file="AkkaService.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Routing;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.CQRS.Infrastructure;
using Akka.CQRS.TradeProcessor.Actors;
using Akka.Routing;
using Akka.Util;
using Microsoft.Extensions.Hosting;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Cluster.Sharding;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace Akka.CQRS.TradePlacers.Service
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
            var config = File.ReadAllText("app.conf");
            var conf = ConfigurationFactory.ParseString(config);

            _actorSystem = ActorSystem.Create("AkkaTrader", AppBootstrap.BootstrapAkka(_provider,
                new AppBootstrapConfig(false, true), conf));

            var tradeRouter = _actorSystem.ActorOf(
                Props.Empty.WithRouter(new ClusterRouterGroup(
                    new ConsistentHashingGroup(new[] { "/user/orderbooks" },
                        TradeEventConsistentHashMapping.TradeEventMapping),
                    new ClusterRouterGroupSettings(10000, new[] { "/user/orderbooks" }, true, useRole: "trade-processor"))), "tradesRouter");

            Cluster.Cluster.Get(_actorSystem).RegisterOnMemberUp(() =>
            {
                var sharding = ClusterSharding.Get(_actorSystem);

                var shardRegionProxy = sharding.StartProxy("orderBook", "trade-processor", new StockShardMsgRouter());
                foreach (var stock in AvailableTickerSymbols.Symbols)
                {
                    var max = (decimal)ThreadLocalRandom.Current.Next(20, 45);
                    var min = (decimal)ThreadLocalRandom.Current.Next(10, 15);
                    var range = new PriceRange(min, 0.0m, max);

                    // start bidders
                    foreach (var i in Enumerable.Repeat(1, ThreadLocalRandom.Current.Next(1, 6)))
                    {
                        _actorSystem.ActorOf(Props.Create(() => new BidderActor(stock, range, shardRegionProxy)));
                    }

                    // start askers
                    foreach (var i in Enumerable.Repeat(1, ThreadLocalRandom.Current.Next(1, 6)))
                    {
                        _actorSystem.ActorOf(Props.Create(() => new AskerActor(stock, range, shardRegionProxy)));
                    }
                }
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