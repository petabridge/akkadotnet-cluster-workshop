using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;

namespace Akka.CQRS.Subscriptions.DistributedPubSub
{
    /// <summary>
    /// Abstract base class for working with <see cref="ITradeEventSubscriptionManager"/>.
    /// </summary>
    public abstract class TradeEventSubscriptionManagerBase : ITradeEventSubscriptionManager
    {
        public Task<TradeSubscribeAck> Subscribe(string tickerSymbol, IActorRef subscriber)
        {
            return Subscribe(tickerSymbol, TradeEventHelpers.AllTradeEventTypes, subscriber);
        }

        public Task<TradeSubscribeAck> Subscribe(string tickerSymbol, TradeEventType @event, IActorRef subscriber)
        {
            return Subscribe(tickerSymbol, new[] { @event }, subscriber);
        }

        public abstract Task<TradeSubscribeAck> Subscribe(string tickerSymbol, TradeEventType[] events, IActorRef subscriber);

        public abstract Task<TradeUnsubscribeAck> Unsubscribe(string tickerSymbol, TradeEventType[] events,
            IActorRef subscriber);

        public Task<TradeUnsubscribeAck> Unsubscribe(string tickerSymbol, TradeEventType @event, IActorRef subscriber)
        {
            return Unsubscribe(tickerSymbol, new[] { @event }, subscriber);
        }

        public Task<TradeUnsubscribeAck> Unsubscribe(string tickerSymbol, IActorRef subscriber)
        {
            return Unsubscribe(tickerSymbol, TradeEventHelpers.AllTradeEventTypes, subscriber);
        }

        internal static string[] ToTopics(string tickerSymbol, TradeEventType[] events)
        {
            return events.Select(x => DistributedPubSubTradeEventTopicFormatter.ToTopic(tickerSymbol, x)).ToArray();
        }
    }

    /// <summary>
    /// <see cref="ITradeEventSubscriptionManager"/> that uses the <see cref="DistributedPubSub.Mediator"/> under the hood.
    /// </summary>
    public sealed class DistributedPubSubTradeEventSubscriptionManager : TradeEventSubscriptionManagerBase
    {
        private readonly IActorRef _mediator;

        public DistributedPubSubTradeEventSubscriptionManager(IActorRef mediator)
        {
            _mediator = mediator;
        }

        public override async Task<TradeSubscribeAck> Subscribe(string tickerSymbol, TradeEventType[] events, IActorRef subscriber)
        {
            var tasks = ToTopics(tickerSymbol, events).Select(x =>
                _mediator.Ask<SubscribeAck>(new Subscribe(x, subscriber), TimeSpan.FromSeconds(3)));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return new TradeSubscribeAck(tickerSymbol, events);
        }

        public override async Task<TradeUnsubscribeAck> Unsubscribe(string tickerSymbol, TradeEventType[] events, IActorRef subscriber)
        {
            var tasks = ToTopics(tickerSymbol, events).Select(x =>
                _mediator.Ask<UnsubscribeAck>(new Unsubscribe(x, subscriber), TimeSpan.FromSeconds(3)));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return new TradeUnsubscribeAck(tickerSymbol, events);
        }


        public static DistributedPubSubTradeEventSubscriptionManager For(ActorSystem sys)
        {
            var mediator = Cluster.Tools.PublishSubscribe.DistributedPubSub.Get(sys).Mediator;
            return new DistributedPubSubTradeEventSubscriptionManager(mediator);
        }
    }
}