using System;
using System.IO;
using System.Linq;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Cluster.Tools.Singleton;
using Akka.Configuration;
using Akka.CQRS.Infrastructure;
using Akka.CQRS.Infrastructure.Ops;
using Akka.CQRS.Pricing.Actors;
using Akka.CQRS.Pricing.Cli;
using Akka.CQRS.Pricing.Commands;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Cluster.Sharding;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;
using static Akka.CQRS.Infrastructure.MongoDbHoconHelper;

namespace Akka.CQRS.Pricing.Service
{
    class Program
    {
        static int Main(string[] args)
        {
            var config = File.ReadAllText("app.conf");
            var conf = ConfigurationFactory.ParseString(config);
            /*
            var actorSystem = ActorSystem.Create("AkkaTrader", conf.BoostrapApplication(new AppBootstrapConfig(true, true)));
            
            var sharding = ClusterSharding.Get(actorSystem);

            
            var shardRegion = sharding.Start("priceAggregator",
                s => Props.Create(() => new MatchAggregator(s)),
                ClusterShardingSettings.Create(actorSystem),
                new StockShardMsgRouter());

            var priceInitiatorActor = actorSystem.ActorOf(ClusterSingletonManager.Props(Props.Create(() => new PriceInitiatorActor(shardRegion)),
                ClusterSingletonManagerSettings.Create(actorSystem).WithRole("pricing-engine").WithSingletonName("priceInitiator")), "priceInitiator");

            var clientHandler =
                actorSystem.ActorOf(Props.Create(() => new ClientHandlerActor(shardRegion)), "subscriptions");

            // make ourselves available to ClusterClient at /user/subscriptions
            ClusterClientReceptionist.Get(actorSystem).RegisterService(clientHandler);

            Cluster.Cluster.Get(actorSystem).RegisterOnMemberUp(() =>
            {
                foreach (var ticker in AvailableTickerSymbols.Symbols)
                {
                    shardRegion.Tell(new Ping(ticker));
                }
            });

            // start Petabridge.Cmd (for external monitoring / supervision)
            var pbm = PetabridgeCmd.Get(actorSystem);
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

            actorSystem.WhenTerminated.Wait();
            */
            return 0;
        }
    }
}
