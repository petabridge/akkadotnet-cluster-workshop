using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using Akka.CQRS.Pricing.Web.Actors;
using Akka.CQRS.Pricing.Web.Hubs;

namespace Akka.CQRS.Pricing.Web.Services
{
    /// <summary>
    /// Used to launch the <see cref="ActorSystem"/> and actors needed
    /// to communicate with the rest of the cluster.
    /// </summary>
    public sealed class AkkaService
    {
        public ActorSystem Sys { get; private set; }

        public Task StartActorSystem(StockHubHelper helper)
        {
            Console.WriteLine("STARTING AKKA.NET");
            var conf = ConfigurationFactory.ParseString(File.ReadAllText("app.conf")).BootstrapFromDocker();

            // need to disable Akka.Cluster
            var finalConfig = ConfigurationFactory.ParseString("akka.actor.provider = remote").WithFallback(conf);

            var actorSystem = Sys = ActorSystem.Create("AkkaCqrsWeb", finalConfig);
            var stockPublisherActor =
                actorSystem.ActorOf(Props.Create(() => new StockPublisherActor(helper)), "stockPublisher");

            var initialContactAddress = Environment.GetEnvironmentVariable("CLUSTER_SEEDS")?.Trim().Split(",")
                .Select(x => Address.Parse(x)).ToList();

            if (initialContactAddress == null)
            {
                actorSystem.Log.Error("No initial cluster contacts found. Please be sure that the CLUSTER_SEEDS environment variable is populated with at least one address.");
                return Task.FromException(new ConfigurationException(
                    "No initial cluster contacts found. Please be sure that the CLUSTER_SEEDS environment variable is populated with at least one address."));
            }

            var configurator = actorSystem.ActorOf(
                Props.Create(() => new StockEventConfiguratorActor(stockPublisherActor, initialContactAddress)),
                "configurator");

            return Task.CompletedTask;
        }

        public async Task Stop()
        {
            await Sys.Terminate();
        }
    }
}
