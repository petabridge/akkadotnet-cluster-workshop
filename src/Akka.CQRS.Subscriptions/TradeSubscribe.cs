using Akka.Actor;

namespace Akka.CQRS.Subscriptions
{
    /// <summary>
    /// Subscribe to trade events for the specified ticker symbol.
    /// </summary>
    public sealed class TradeSubscribe
    {
        public TradeSubscribe(string tickerSymbol, TradeEventType[] events, IActorRef subscriber)
        {
            TickerSymbol = tickerSymbol;
            Events = events;
            Subscriber = subscriber;
        }

        public string TickerSymbol { get; }

        public TradeEventType[] Events { get; }

        public IActorRef Subscriber { get; }
    }
}