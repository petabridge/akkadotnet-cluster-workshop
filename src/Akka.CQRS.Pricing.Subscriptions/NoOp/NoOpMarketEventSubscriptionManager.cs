using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.CQRS.Pricing.Subscriptions.NoOp
{
    /// <summary>
    /// No-op market event subscription manager. Does nothing.
    /// </summary>
    public sealed class NoOpMarketEventSubscriptionManager : MarketEventSubscriptionManagerBase
    {
        public static readonly NoOpMarketEventSubscriptionManager Instance = new NoOpMarketEventSubscriptionManager();
        private NoOpMarketEventSubscriptionManager() { }

        public override Task<MarketSubscribeAck> Subscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            return Task.FromResult(new MarketSubscribeAck(tickerSymbol, events));
        }

        public override Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            return Task.FromResult(new MarketUnsubscribeAck(tickerSymbol, events));
        }
    }
}