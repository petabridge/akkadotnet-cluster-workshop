namespace Akka.CQRS.Subscriptions
{
    /// <summary>
    /// Unsubscription to a specific ticker has been successful.
    /// </summary>
    public sealed class TradeUnsubscribeAck : IWithStockId
    {
        public TradeUnsubscribeAck(string stockId, TradeEventType[] events)
        {
            StockId = stockId;
            Events = events;
        }

        public string StockId { get; }

        public TradeEventType[] Events { get; }
    }
}