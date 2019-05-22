using Akka.Actor;

namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Subscribe to trade events for the specified ticker symbol.
    /// </summary>
    public sealed class MarketSubscribe : IWithStockId
    {
        public MarketSubscribe(string stockId, MarketEventType[] events, IActorRef subscriber)
        {
            StockId = stockId;
            Events = events;
            Subscriber = subscriber;
        }

        public string StockId { get; }

        public MarketEventType[] Events { get; }

        public IActorRef Subscriber { get; }
    }
}