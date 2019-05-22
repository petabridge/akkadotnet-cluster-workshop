namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Unsubscription to a specific ticker has been successful.
    /// </summary>
    public sealed class MarketUnsubscribeAck
    {
        public MarketUnsubscribeAck(string tickerSymbol, MarketEventType[] events)
        {
            TickerSymbol = tickerSymbol;
            Events = events;
        }

        public string TickerSymbol { get; }

        public MarketEventType[] Events { get; }
    }
}