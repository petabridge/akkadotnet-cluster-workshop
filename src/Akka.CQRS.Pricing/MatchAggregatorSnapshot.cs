using System;
using System.Collections.Generic;
using System.Text;
using Akka.CQRS.Pricing.Events;

namespace Akka.CQRS.Pricing
{
    /// <summary>
    /// Represents the point-in-time state of the match aggregator at any given time.
    /// </summary>
    public sealed class MatchAggregatorSnapshot
    {
        public MatchAggregatorSnapshot(decimal avgPrice, double avgVolume, 
            IReadOnlyList<PriceChanged> recentPriceUpdates, IReadOnlyList<VolumeChanged> recentVolumeUpdates)
        {
            AvgPrice = avgPrice;
            AvgVolume = avgVolume;
            RecentPriceUpdates = recentPriceUpdates;
            RecentVolumeUpdates = recentVolumeUpdates;
        }

        /// <summary>
        /// The most recently saved average price.
        /// </summary>
        public decimal AvgPrice { get; }

        /// <summary>
        /// The most recently saved average volume.
        /// </summary>
        public double AvgVolume { get; }

        public IReadOnlyList<IPriceUpdate> RecentPriceUpdates { get; }

        public IReadOnlyList<IVolumeUpdate> RecentVolumeUpdates { get; }
    }
}
