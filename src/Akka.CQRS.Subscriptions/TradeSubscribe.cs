using Akka.Actor;

namespace Akka.CQRS.Subscriptions
{
    /// <summary>
    /// Subscribe to trade events for the specified ticker symbol.
    /// </summary>
    public sealed class TradeSubscribe : IWithStockId
    {
        public TradeSubscribe(string stockId, TradeEventType[] events, IActorRef subscriber)
        {
            StockId = stockId;
            Events = events;
            Subscriber = subscriber;
        }

        public string StockId { get; }

        public TradeEventType[] Events { get; }

        public IActorRef Subscriber { get; }
    }
}