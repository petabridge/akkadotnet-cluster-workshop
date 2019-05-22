namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Unsubscribe from a specific ticker was not successful.
    /// </summary>
    public sealed class MarketUnsubscribeNack : IWithStockId
    {
        public MarketUnsubscribeNack(string stockId, MarketEventType[] events, string reason)
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