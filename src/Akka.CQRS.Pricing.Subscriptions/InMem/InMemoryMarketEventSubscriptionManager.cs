using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.CQRS.Pricing.Subscriptions.InMem
{
    /// <summary>
    /// In-memory subscription manager + publisher.
    /// </summary>
    public sealed class InMemoryMarketEventSubscriptionManager : MarketEventSubscriptionManagerBase, IMarketEventPublisher
    {
        private readonly Dictionary<MarketEventType, HashSet<IActorRef>> _subscribers;

        public InMemoryMarketEventSubscriptionManager()
            : this(new Dictionary<MarketEventType, HashSet<IActorRef>>()) { }

        public InMemoryMarketEventSubscriptionManager(Dictionary<MarketEventType, HashSet<IActorRef>> subscribers)
        {
            _subscribers = subscribers;
        }

        public override Task<MarketSubscribeAck> Subscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            foreach (var e in events)
            {
                EnsureSub(e);
                _subscribers[e].Add(subscriber);
            }

            return Task.FromResult(new MarketSubscribeAck(tickerSymbol, events));
        }

        public override Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            foreach (var e in events)
            {
                EnsureSub(e);
                _subscribers[e].Remove(subscriber);
            }

            return Task.FromResult(new MarketUnsubscribeAck(tickerSymbol, events));
        }

        public void Publish(string tickerSymbol, IMarketEvent @event)
        {
            var eventType = @event.ToMarketEventType();
            EnsureSub(eventType);

            foreach (var sub in _subscribers[eventType])
                sub.Tell(@event);
        }

        private void EnsureSub(MarketEventType e)
        {
            if (!_subscribers.ContainsKey(e))
            {
                _subscribers[e] = new HashSet<IActorRef>();
            }
        }
    }
}