using System;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.CQRS.Infrastructure.Ops;
using Akka.CQRS.Serialization;
using static Akka.CQRS.Infrastructure.MongoDbHoconHelper;
using static Akka.CQRS.Infrastructure.Ops.OpsConfig;
#if PHOBOS
using Phobos.Actor;
#endif

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
                .WithFallback(GetOpsConfig())
                .WithFallback(TradeEventSerializer.Config)
                //.WithFallback(ClusterSharding.DefaultConfig())
                //.WithFallback(DistributedData.DistributedData.DefaultConfig()) // needed for DData sharding
                //.WithFallback(ClusterClientReceptionist.DefaultConfig())
                //.WithFallback(DistributedPubSub.DefaultConfig())
                .BootstrapFromDocker();


#if PHOBOS
            return config.BootstrapPhobos(appConfig);
#else

            if (!appConfig.NeedClustering)
            {
                return ConfigurationFactory.ParseString("akka.actor.provider = remote").WithFallback(config);
            }


            return config;
#endif
        }

#if PHOBOS
        public const string ENABLE_PHOBOS = "ENABLE_PHOBOS";

        /// <summary>
        ///     Name of the <see cref="Environment" /> variable used to direct Phobos' StatsD
        ///     output.
        /// </summary>
        public const string STATSD_URL = "STATSD_URL";

        /// <summary>
        ///     Name of the <see cref="Environment" /> variable used to direct Phobos' StatsD
        ///     output.
        /// </summary>
        public const string STATSD_PORT = "STATSD_PORT";

        /// <summary>
        ///     Name of the <see cref="Environment" /> variable used to direct Phobos' Jaeger
        ///     output.
        /// </summary>
        public const string JAEGER_AGENT_HOST = "JAEGER_AGENT_HOST";

        public static Config BootstrapPhobos(this Config c, AppBootstrapConfig appConfig)
        {
            var enablePhobos = Environment.GetEnvironmentVariable(ENABLE_PHOBOS);
            if (!bool.TryParse(enablePhobos, out var phobosEnabled))
            {
                // don't turn on Phobos
                return c;
            }
            else if (!phobosEnabled)
            {
                // don't turn on Phobos
                return c;
            }

            var phobosConfig = GetPhobosConfig();

            var statsdUrl = Environment.GetEnvironmentVariable(STATSD_URL);
            var statsDPort = Environment.GetEnvironmentVariable(STATSD_PORT);
            var jaegerAgentHost = Environment.GetEnvironmentVariable(JAEGER_AGENT_HOST);

            if (!string.IsNullOrEmpty(statsdUrl) && int.TryParse(statsDPort, out var portNum))
                phobosConfig = ConfigurationFactory.ParseString($"phobos.monitoring.statsd.endpoint=\"{statsdUrl}\"" +
                                                                Environment.NewLine +
                                                                $"phobos.monitoring.statsd.port={portNum}" +
                                                                Environment.NewLine +
                                                                $"phobos.tracing.jaeger.agent.host={jaegerAgentHost}")
                    .WithFallback(phobosConfig);

            if (!appConfig.NeedClustering)
            {
                var config = ConfigurationFactory.ParseString(@"akka.actor.provider = ""Phobos.Actor.Remote.PhobosRemoteActorRefProvider, Phobos.Actor.Remote""");
                return config.WithFallback(phobosConfig).WithFallback(c);
            }

            return phobosConfig.WithFallback(c);
        }

#endif
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
