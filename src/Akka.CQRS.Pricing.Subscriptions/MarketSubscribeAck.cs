namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Subscription to a specific ticker has been successful.
    /// </summary>
    public sealed class MarketSubscribeAck : IWithStockId
    {
        public MarketSubscribeAck(string stockId, MarketEventType[] events)
        {
            StockId = stockId;
            Events = events;
        }

        public string StockId { get; }

        public MarketEventType[] Events { get; }
    }
}