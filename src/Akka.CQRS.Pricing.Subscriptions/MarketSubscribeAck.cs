namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Subscription to a specific ticker has been successful.
    /// </summary>
    public sealed class MarketSubscribeAck
    {
        public MarketSubscribeAck(string tickerSymbol, MarketEventType[] events)
        {
            TickerSymbol = tickerSymbol;
            Events = events;
        }

        public string TickerSymbol { get; }

        public MarketEventType[] Events { get; }
    }
}