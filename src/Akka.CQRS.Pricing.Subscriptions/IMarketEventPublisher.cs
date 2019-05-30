namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Abstraction for publishing data about <see cref="IMarketEvent"/> instances.
    /// </summary>
    public interface IMarketEventPublisher
    {
        void Publish(string tickerSymbol, IMarketEvent @event);
    }
}