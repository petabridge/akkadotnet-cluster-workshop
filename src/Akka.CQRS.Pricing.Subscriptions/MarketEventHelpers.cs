using System;
using System.Linq;
using Akka.CQRS.Pricing.Events;

namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Extension methods for working with <see cref="ITradeEvent"/>
    /// </summary>
    public static class MarketEventHelpers
    {
        public static readonly MarketEventType[] AllMarketEventTypes =
            Enum.GetValues(typeof(MarketEventType)).Cast<MarketEventType>().ToArray();

        public static MarketEventType ToMarketEventType(this IMarketEvent @event)
        {
            switch (@event)
            {
                case IPriceUpdate p:
                    return MarketEventType.PriceChange;
                case IVolumeUpdate v:
                    return MarketEventType.VolumeChange;
                default:
                    throw new ArgumentOutOfRangeException($"[{@event}] is not a supported market event type.", nameof(@event));
            }
        }
    }
}