using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.CQRS.Subscriptions.DistributedPubSub;

namespace Akka.CQRS.Subscriptions
{
    /// <inheritdoc />
    /// <summary>
    /// Abstract base class for working with <see cref="T:Akka.CQRS.Subscriptions.ITradeEventSubscriptionManager" />.
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
}