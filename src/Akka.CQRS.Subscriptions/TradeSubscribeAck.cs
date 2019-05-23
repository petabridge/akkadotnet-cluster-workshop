namespace Akka.CQRS.Subscriptions
{
    /// <summary>
    /// Subscription to a specific ticker has been successful.
    /// </summary>
    public sealed class TradeSubscribeAck : IWithStockId
    {
        public TradeSubscribeAck(string stockId, TradeEventType[] events)
        {
            StockId = stockId;
            Events = events;
        }

        public string StockId { get; }

        public TradeEventType[] Events { get; }
    }
}