namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Unsubscribe from a specific ticker was not successful.
    /// </summary>
    public sealed class MarketUnsubscribeNack
    {
        public MarketUnsubscribeNack(string tickerSymbol, MarketEventType[] events, string reason)
        {
            TickerSymbol = tickerSymbol;
            Events = events;
            Reason = reason;
        }

        public string TickerSymbol { get; }

        public MarketEventType[] Events { get; }

        public string Reason { get; }
    }
}