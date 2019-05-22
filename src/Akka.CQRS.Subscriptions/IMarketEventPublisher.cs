using Akka.CQRS.Pricing;

namespace Akka.CQRS.Subscriptions
{
    /// <summary>
    /// Abstraction for publishing data about <see cref="IMarketEvent"/> instances.
    /// </summary>
    public interface IMarketEventPublisher
    {
        void Publish(string tickerSymbol, ITradeEvent @event);
    }
}