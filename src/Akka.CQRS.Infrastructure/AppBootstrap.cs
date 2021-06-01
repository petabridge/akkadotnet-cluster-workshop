using System;
using System.Linq;
using System.Net;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.Client;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.CQRS.Infrastructure.Ops;
using Akka.CQRS.Serialization;
using Akka.DependencyInjection;
using App.Metrics;
using App.Metrics.Formatters.Prometheus;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTracing;
using static Akka.CQRS.Infrastructure.MongoDbHoconHelper;
using static Akka.CQRS.Infrastructure.Ops.OpsConfig;
#if PHOBOS // to stop ReSharper et al from blowing up our usings needed for Phobos builds
using Microsoft.Extensions.Logging;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Jaeger.Senders.Thrift;
using System.Reflection;
using Akka.Persistence.Extras;
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
        /// <summary>
        /// Adds monitoring and tracing support for any application that calls it.
        /// </summary>
        /// <param name="services">The services collection being configured by the application.</param>
        /// <returns>The original <see cref="IServiceCollection"/></returns>
        public static void AddPhobosApm(this IServiceCollection services)
        {
            // enables OpenTracing for ASP.NET / .NET Core
            services.AddOpenTracing(o =>
            {
                o.ConfigureAspNetCore(a =>
                {
                    a.Hosting.OperationNameResolver = context => $"{context.Request.Method} {context.Request.Path}";

                    // skip Prometheus HTTP /metrics collection from appearing in our tracing system
                    a.Hosting.IgnorePatterns.Add(x => x.Request.Path.StartsWithSegments(new PathString("/metrics")));
                });
                o.ConfigureGenericDiagnostics(c => { });
            });
            services.ConfigureAppMetrics();
            services.ConfigureJaegerTracing();
        }

        /// <summary>
        ///     Name of the <see cref="Environment" /> variable used to direct Phobos' Jaeger
        ///     output.
        ///     See https://github.com/jaegertracing/jaeger-client-csharp for details.
        /// </summary>
        public const string JaegerAgentHostEnvironmentVar = "JAEGER_AGENT_HOST";

        public const string JaegerEndpointEnvironmentVar = "JAEGER_ENDPOINT";

        public const string JaegerAgentPortEnvironmentVar = "JAEGER_AGENT_PORT";

        public const int DefaultJaegerAgentPort = 6831;

        public static void ConfigureAppMetrics(this IServiceCollection services)
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

        public static void ConfigureJaegerTracing(this IServiceCollection services)
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
                var tracer = new Tracer.Builder(Assembly.GetEntryAssembly().GetName().Name)
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

        public static Config BootstrapConfig(this Config c, AppBootstrapConfig appConfig)
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


            if (!appConfig.NeedClustering)
            {
                return ConfigurationFactory.ParseString("akka.actor.provider = remote").WithFallback(config);
            }


            return config;
        }

        public const string ENABLE_PHOBOS = "ENABLE_PHOBOS";

        public static ActorSystemSetup BootstrapAkka(IServiceProvider sp, AppBootstrapConfig appConfig,
            Config baseConfig)
        {
            var enablePhobos = Environment.GetEnvironmentVariable(ENABLE_PHOBOS);
            var config = baseConfig.BootstrapConfig(appConfig);

            // for DI support
            var serviceProviderSetup = ServiceProviderSetup.Create(sp);

#if !PHOBOS
            return BootstrapSetup.Create().WithConfig(config).And(serviceProviderSetup);
#else


            if (!bool.TryParse(enablePhobos, out var phobosEnabled) || !phobosEnabled)
            {
                // don't turn on Phobos
                return BootstrapSetup.Create().WithConfig(config).And(serviceProviderSetup);
            }


            // load Phobos HOCON
            /*
             * Phobos 1.0+ doesn't use much HOCON anymore, but there are still a number of useful options
             * that you may want to enable, such as monitoring the length of your mailbox queue - which
             * still need to be turned on this way.
             *
             * Further reading: https://phobos.petabridge.com/articles/setup/configuration.html#hocon
             */
            var phobosHocon = GetPhobosConfig().WithFallback(config);
            var metrics = sp.GetRequiredService<IMetricsRoot>();
            var tracing = sp.GetRequiredService<ITracer>();

            var phobosConfig = new PhobosConfigBuilder()
                .WithMetrics(a => a.SetMetricsRoot(metrics))
                .WithTracing(t => t.SetTracer(tracing)
                    // trace filtering: https://phobos.petabridge.com/articles/setup/configuration.html#filtering-traced-messages-with-phobosconfigbuilder
                    // eliminate noise from our Jaeger traces
                    .AddMessageFilter(o => o is IWithStockId || o is IConfirmableMessage));

            var phobosSetup = PhobosSetup.Create(phobosConfig);

            // technically not needed - the Phobos HOCON already specifies this
            // but this is how the BootstrapSetup can be used to determine, via code,
            // how to configure the akka.actor.provider
            ProviderSelection actorRefProvider = PhobosProviderSelection.Cluster;

            if (!appConfig.NeedClustering)
            {
                actorRefProvider = PhobosProviderSelection.Remote;
            }

            return phobosSetup.WithSetup(BootstrapSetup.Create().WithConfig(phobosHocon)
                .WithActorRefProvider(actorRefProvider)).And(serviceProviderSetup);

#endif
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
