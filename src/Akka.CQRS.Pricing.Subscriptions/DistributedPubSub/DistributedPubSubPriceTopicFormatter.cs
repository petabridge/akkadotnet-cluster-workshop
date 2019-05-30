using System;

namespace Akka.CQRS.Pricing.Subscriptions.DistributedPubSub
{
    /// <summary>
    /// Helper methods for working with price and volume updates.
    /// </summary>
    public static class DistributedPubSubPriceTopicFormatter
    {
        public static string PriceUpdateTopic(string tickerSymbol)
        {
            return $"{tickerSymbol}-price";
        }

        public static string VolumeUpdateTopic(string tickerSymbol)
        {
            return $"{tickerSymbol}-update";
        }

        public static string ToTopic(string tickerSymbol, MarketEventType marketEventType)
        {
            string ToStr(MarketEventType e)
            {
                switch (e)
                {
                    case MarketEventType.PriceChange:
                        return "price";
                    case MarketEventType.VolumeChange:
                        return "volume";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(e));
                }
            }
            return $"{tickerSymbol}-{ToStr(marketEventType)}";
        }
    }
}
