using System;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.CQRS.Infrastructure.Ops;
using Akka.CQRS.Serialization;
using static Akka.CQRS.Infrastructure.MongoDbHoconHelper;

namespace Akka.CQRS.Infrastructure
{
    /// <summary>
    /// Used to boostrap the start of <see cref="ActorSystem"/>s by injecting their configs
    /// with the relevant bits and pieces.
    /// </summary>
    public static class AppBootstrap
    {
        public static Config BoostrapApplication(this Config c, AppBootstrapConfig appConfig)
        {
            var config = c;
            if (appConfig.NeedPersistence)
            {
                var mongoConnectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STR")?.Trim();
                if (string.IsNullOrEmpty(mongoConnectionString))
                {
                    Console.WriteLine("ERROR! MongoDb connection string not provided. Can't start.");
                    throw new ConfigurationException("ERROR! MongoDb connection string not provided. Can't start.");
                }
                else
                {
                    Console.WriteLine("Connecting to MongoDb at {0}", mongoConnectionString);
                }

                config = c.WithFallback(GetMongoHocon(mongoConnectionString));
            }

            config = config
                .WithFallback(OpsConfig.GetOpsConfig())
                .WithFallback(TradeEventSerializer.Config)
                .WithFallback(ClusterSharding.DefaultConfig())
                .WithFallback(ClusterClientReceptionist.DefaultConfig())
                .WithFallback(DistributedPubSub.DefaultConfig())
                .BootstrapFromDocker();

            if (!appConfig.NeedClustering)
            {
                return ConfigurationFactory.ParseString("akka.actor.provider = remote").WithFallback(config);
            }

            return config;
        }
    }

    public sealed class AppBootstrapConfig
    {
        public AppBootstrapConfig(bool needPersistence = true, bool needClustering = true)
        {
            NeedPersistence = needPersistence;
            NeedClustering = needClustering;
        }

        public bool NeedPersistence { get; }

        public bool NeedClustering { get; }
    }
}
