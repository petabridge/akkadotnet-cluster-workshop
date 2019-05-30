namespace Akka.CQRS.Subscriptions
{
    /// <summary>
    /// Subscription to a specific ticker was not successful.
    /// </summary>
    public sealed class TradeSubscribeNack : IWithStockId
    {
        public TradeSubscribeNack(string stockId, TradeEventType[] events, string reason)
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