using System;
using System.Net;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.CQRS.Infrastructure.Ops;
using Akka.CQRS.Serialization;
using App.Metrics;
using App.Metrics.Formatters.Prometheus;
using Jaeger;
using Jaeger.Core;
using Jaeger.Core.Reporters;
using Jaeger.Core.Samplers;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Jaeger.Senders.Thrift;
using Jaeger.Transport.Thrift.Transport.Sender;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTracing;
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
        /// <summary>
        /// Adds monitoring and tracing support for any application that calls it.
        /// </summary>
        /// <param name="services">The services collection being configured by the application.</param>
        /// <returns>The original <see cref="IServiceCollection"/></returns>
        public static IServiceCollection BootstrapApm(this IServiceCollection services)
        {
            return services;
        }

        /// <summary>
        ///     Name of the <see cref="Environment" /> variable used to direct Phobos' Jaeger
        ///     output.
        ///     See https://github.com/jaegertracing/jaeger-client-csharp for details.
        /// </summary>
        public const string JaegerAgentHostEnvironmentVar = "JAEGER_AGENT_HOST";

        public const string JaegerEndpointEnvironmentVar = "JAEGER_ENDPOINT";

        public const string JaegerAgentPortEnvironmentVar = "JAEGER_AGENT_PORT";

        public const int DefaultJaegerAgentPort = 6832;

        public static void ConfigureAppMetrics(IServiceCollection services)
        {
            services.AddMetricsTrackingMiddleware();
            services.AddMetrics(b =>
            {
                var metrics = b.Configuration.Configure(o =>
                {
                    o.GlobalTags.Add("host", Dns.GetHostName());
                    o.DefaultContextLabel = "akka.net";
                    o.Enabled = true;
                    o.ReportingEnabled = true;
                })
                    .OutputMetrics.AsPrometheusPlainText()
                    .Build();

                services.AddMetricsEndpoints(ep =>
                {
                    ep.MetricsTextEndpointOutputFormatter = metrics.OutputMetricsFormatters
                        .OfType<MetricsPrometheusTextOutputFormatter>().First();
                    ep.MetricsEndpointOutputFormatter = metrics.OutputMetricsFormatters
                        .OfType<MetricsPrometheusTextOutputFormatter>().First();
                });
            });
            services.AddMetricsReportingHostedService();
        }

        public static void ConfigureJaegerTracing(IServiceCollection services)
        {
#if PHOBOS
            ISender BuildSender()
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(JaegerEndpointEnvironmentVar)))
                {
                    if (!int.TryParse(Environment.GetEnvironmentVariable(JaegerAgentPortEnvironmentVar),
                        out var udpPort))
                        udpPort = DefaultJaegerAgentPort;
                    return new UdpSender(
                        Environment.GetEnvironmentVariable(JaegerAgentHostEnvironmentVar) ?? "localhost",
                        udpPort, 0);
                }

                return new HttpSender(Environment.GetEnvironmentVariable(JaegerEndpointEnvironmentVar));
            }

            services.AddSingleton<ITracer>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

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
                var tracer = new Tracer.Builder(typeof(Startup).Assembly.GetName().Name)
                    .WithReporter(new CompositeReporter(remoteReporter, logReporter))
                    .WithSampler(sampler)
                    .WithScopeManager(
                        new ActorScopeManager()); // IMPORTANT: ActorScopeManager needed to properly correlate trace inside Akka.NET

                return tracer.Build();
            });
#else
            services.AddSingleton<ITracer>(OpenTracing.Util.GlobalTracer.Instance);
#endif
        }

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
                .WithFallback(ClusterSharding.DefaultConfig())
                .WithFallback(DistributedData.DistributedData.DefaultConfig()) // needed for DData sharding
                .WithFallback(ClusterClientReceptionist.DefaultConfig())
                .WithFallback(DistributedPubSub.DefaultConfig())
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

        public static ActorSystemSetup BootstrapPhobos(this IServiceCollection services, AppBootstrapConfig appConfig)
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
