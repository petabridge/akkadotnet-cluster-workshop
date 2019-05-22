using Akka.Actor;

namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Unsubscribe to trade events for the specified ticker symbol.
    /// </summary>
    public sealed class MarketUnsubscribe
    {
        public MarketUnsubscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            TickerSymbol = tickerSymbol;
            Events = events;
            Subscriber = subscriber;
        }

        public string TickerSymbol { get; }

        public MarketEventType[] Events { get; }

        public IActorRef Subscriber { get; }
    }
}