namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// The type of market event we're interested in.
    /// </summary>
    public enum MarketEventType
    {
        VolumeChange,
        PriceChange,
    }
}
