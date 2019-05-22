namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Subscription to a specific ticker was not successful.
    /// </summary>
    public sealed class MarketSubscribeNack : IWithStockId
    {
        public MarketSubscribeNack(string stockId, MarketEventType[] events, string reason)
        {
            StockId = stockId;
            Events = events;
            Reason = reason;
        }

        public string StockId { get; }

        public MarketEventType[] Events { get; }

        public string Reason { get; }
    }
}