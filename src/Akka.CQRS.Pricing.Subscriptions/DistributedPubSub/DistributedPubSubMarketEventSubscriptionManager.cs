using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;

namespace Akka.CQRS.Pricing.Subscriptions.DistributedPubSub
{
    /// <summary>
    /// <see cref="IMarketEventSubscriptionManager"/> that uses the <see cref="DistributedPubSub.Mediator"/> under the hood.
    /// </summary>
    public sealed class DistributedPubSubMarketEventSubscriptionManager : MarketEventSubscriptionManagerBase
    {
        private readonly IActorRef _mediator;

        public DistributedPubSubMarketEventSubscriptionManager(IActorRef mediator)
        {
            _mediator = mediator;
        }

        public override async Task<MarketSubscribeAck> Subscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            var tasks = ToTopics(tickerSymbol, events).Select(x =>
                _mediator.Ask<SubscribeAck>(new Subscribe(x, subscriber), TimeSpan.FromSeconds(3)));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return new MarketSubscribeAck(tickerSymbol, events);
        }

        public override async Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            var tasks = ToTopics(tickerSymbol, events).Select(x =>
                _mediator.Ask<UnsubscribeAck>(new Unsubscribe(x, subscriber), TimeSpan.FromSeconds(3)));

            await Task.WhenAll(tasks).ConfigureAwait(false);
            
            return new MarketUnsubscribeAck(tickerSymbol, events);
        }

        internal static string[] ToTopics(string tickerSymbol, MarketEventType[] events)
        {
            return events.Select(x => DistributedPubSubPriceTopicFormatter.ToTopic(tickerSymbol, x)).ToArray();
        }

        public static DistributedPubSubMarketEventSubscriptionManager For(ActorSystem system)
        {
            return new DistributedPubSubMarketEventSubscriptionManager(Cluster.Tools.PublishSubscribe.DistributedPubSub
                .Get(system).Mediator);
        }
    }
}