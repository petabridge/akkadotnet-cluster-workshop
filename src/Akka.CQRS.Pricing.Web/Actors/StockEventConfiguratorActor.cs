using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Tools.Client;
using Akka.CQRS.Pricing.Subscriptions.Client;
using Akka.Event;

namespace Akka.CQRS.Pricing.Web.Actors
{
    /// <summary>
    /// Uses the <see cref="ClusterClient"/> to begin publishing events to <see cref="StockPublisherActor"/>.
    /// </summary>
    public class StockEventConfiguratorActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private IActorRef _clusterClient;
        private readonly IActorRef _stockPublisher;
        private ImmutableHashSet<ActorPath> _initialContacts;

        private sealed class Start
        {
            public static readonly Start Instance = new Start();
            private Start() { }
        }

        public StockEventConfiguratorActor(IActorRef stockPublisher, IReadOnlyList<Address> contactAddresses)
        {
            _initialContacts = contactAddresses.Select(x => new RootActorPath(x) / "user" / "subscriptions").ToImmutableHashSet();
            _stockPublisher = stockPublisher;

            Receive<Start>(s =>
            {
                _log.Info("Contacting cluster client on addresses [{0}]", string.Join(",", _initialContacts));
                _clusterClient.Tell(new SubscribeClientAll(), _stockPublisher);
            });
        }

        protected override void PreStart()
        {
            Self.Tell(Start.Instance);
            _clusterClient = Context.ActorOf(Akka.Cluster.Tools.Client.ClusterClient.Props(ClusterClientSettings
                .Create(Context.System)
                .WithInitialContacts(_initialContacts)));
        }
    }
}
