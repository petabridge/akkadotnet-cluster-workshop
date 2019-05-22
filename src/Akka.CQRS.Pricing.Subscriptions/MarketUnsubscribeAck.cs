namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Unsubscription to a specific ticker has been successful.
    /// </summary>
    public sealed class MarketUnsubscribeAck : IWithStockId
    {
        public MarketUnsubscribeAck(string stockId, MarketEventType[] events)
        {
            StockId = stockId;
            Events = events;
        }

        public string StockId { get; }

        public MarketEventType[] Events { get; }
    }
}