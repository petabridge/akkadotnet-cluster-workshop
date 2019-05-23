namespace Akka.CQRS.Subscriptions
{
    /// <summary>
    /// Unsubscribe from a specific ticker was not successful.
    /// </summary>
    public sealed class TradeUnsubscribeNack : IWithStockId
    {
        public TradeUnsubscribeNack(string stockId, TradeEventType[] events, string reason)
        {
            StockId = stockId;
            Events = events;
            Reason = reason;
        }

        public string StockId { get; }

        public TradeEventType[] Events { get; }

        public string Reason { get; }
    }
}