using System;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.CQRS.Infrastructure.Ops;
using Akka.CQRS.Serialization;
using App.Metrics;
using App.Metrics.Reporting.Graphite;
using App.Metrics.Reporting.Graphite.Client;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Mock;
using static Akka.CQRS.Infrastructure.MongoDbHoconHelper;
using static Akka.CQRS.Infrastructure.Ops.OpsConfig;

#if PHOBOS
using System.Net;
using Phobos.Actor;
using Phobos.Actor.Configuration;
using Phobos.Tracing.Scopes;
#endif

namespace Akka.CQRS.Infrastructure
{
    /// <summary>
    /// Used to boostrap the start of <see cref="ActorSystem"/>s by injecting their configs
    /// with the relevant bits and pieces.
    /// </summary>
    public static class AppBootstrap
    {
        public static ActorSystemSetup BoostrapApplication(this BootstrapSetup setup, Config config, AppBootstrapConfig appConfig)
        {
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

                config = config.WithFallback(GetMongoHocon(mongoConnectionString));
            }

            config = config
                .WithFallback(GetOpsConfig())
                .WithFallback(TradeEventSerializer.Config)
                .WithFallback(ClusterSharding.DefaultConfig())
                .WithFallback(DistributedData.DistributedData.DefaultConfig()) // needed for DData sharding
                .WithFallback(ClusterClientReceptionist.DefaultConfig())
                .WithFallback(DistributedPubSub.DefaultConfig())
                .BootstrapFromDocker();


#if PHOBOS
            
            return setup.BootstrapPhobos(config, appConfig);
#else

            if (!appConfig.NeedClustering)
            {
                config = ConfigurationFactory.ParseString("akka.actor.provider = remote").WithFallback(config);
            }

            return ActorSystemSetup.Create(setup.WithConfig(config));
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
        /// <summary>
        ///     Name of the <see cref="Environment" /> variable used to direct Phobos' Jaeger
        ///     output endpoint port setup.
        /// </summary>
        public const string JAEGER_AGENT_PORT = "JAEGER_AGENT_PORT";

        public const int DefaultJaegerAgentPort = 6832;

        public static ActorSystemSetup BootstrapPhobos(this BootstrapSetup setup, Config config, AppBootstrapConfig appConfig)
        {
            var enablePhobos = Environment.GetEnvironmentVariable(ENABLE_PHOBOS);
            if (!bool.TryParse(enablePhobos, out var phobosEnabled))
            {
                // don't turn on Phobos
                return ActorSystemSetup.Create(setup.WithConfig(config));
            }
            else if (!phobosEnabled)
            {
                // don't turn on Phobos
                return ActorSystemSetup.Create(setup.WithConfig(config));
            }

            var phobosConfig = GetPhobosConfig();
            config = phobosConfig.WithFallback(config);

            OpenTracing.ITracer tracer = CreateJaegerTracing(appConfig);
            
            var statsdUrl = Environment.GetEnvironmentVariable(STATSD_URL);
            var statsDPort = Environment.GetEnvironmentVariable(STATSD_PORT);
             App.Metrics.IMetricsRoot metrics = new MetricsBuilder().Configuration.Configure(o =>
            {
                o.GlobalTags.Add("host", Dns.GetHostName());
                o.DefaultContextLabel = "akka.net";
                o.Enabled = true;
                o.ReportingEnabled = true;
            }).Report.ToGraphite(
                 options => {
                     options.Graphite.BaseUri = new Uri($"net.udp://{statsdUrl}:{statsDPort}");
                     options.ClientPolicy.BackoffPeriod = TimeSpan.FromSeconds(30);
                     options.ClientPolicy.FailuresBeforeBackoff = 5;
                     options.ClientPolicy.Timeout = TimeSpan.FromSeconds(10);
                     options.FlushInterval = TimeSpan.FromSeconds(5);
                 }).Build();
            
            var phobosConfigBuilder = new PhobosConfigBuilder()
                .WithMetrics(m => m.SetMetricsRoot(metrics))
                .WithTracing(t => t.SetTracer(tracer));
            
            return PhobosSetup.Create(phobosConfigBuilder)
                .WithSetup(setup
                    .WithConfig(config)
                    .WithActorRefProvider(appConfig.NeedClustering
                        ? PhobosProviderSelection.Cluster
                        : PhobosProviderSelection.Remote));

        }
        
        public static ITracer CreateJaegerTracing(AppBootstrapConfig config)
        {
            static ISender BuildSender()
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(JAEGER_AGENT_HOST)))
                {
                    if (!int.TryParse(Environment.GetEnvironmentVariable(JAEGER_AGENT_PORT),
                        out var udpPort))
                        udpPort = DefaultJaegerAgentPort;
                    return new UdpSender(
                        Environment.GetEnvironmentVariable(JAEGER_AGENT_HOST) ?? "localhost",
                        udpPort, 0);
                }

                return new HttpSender(Environment.GetEnvironmentVariable(JAEGER_AGENT_HOST));
            }

            var loggerFactory = new LoggerFactory();
            var builder = BuildSender();
            var logReporter = new LoggingReporter(loggerFactory);

            var remoteReporter = new RemoteReporter.Builder()
                .WithLoggerFactory(loggerFactory) // optional, defaults to no logging
                .WithMaxQueueSize(100) // optional, defaults to 100
                .WithFlushInterval(TimeSpan.FromSeconds(1)) // optional, defaults to TimeSpan.FromSeconds(1)
                .WithSender(builder) // optional, defaults to UdpSender("localhost", 6831, 0)
                .Build();

            var sampler = new ConstSampler(true); // keep sampling disabled

            // name the service after the executing assembly
            var tracer = new Tracer.Builder(config.ServiceName)
                .WithReporter(new CompositeReporter(remoteReporter, logReporter))
                .WithSampler(sampler)
                .WithScopeManager(
                    new ActorScopeManager()); // IMPORTANT: ActorScopeManager needed to properly correlate trace inside Akka.NET

            return tracer.Build();
        }

#endif
    }

    public sealed class AppBootstrapConfig
    {
        public AppBootstrapConfig(string serviceName, bool needPersistence = true, bool needClustering = true)
        {
            ServiceName = serviceName;
            NeedPersistence = needPersistence;
            NeedClustering = needClustering;
        }

        public bool NeedPersistence { get; }

        public bool NeedClustering { get; }
        public string ServiceName { get; }
    }
}
