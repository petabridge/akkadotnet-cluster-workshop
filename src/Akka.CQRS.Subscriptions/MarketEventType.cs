using System;
using System.Collections.Generic;
using System.Text;

namespace Akka.CQRS.Subscriptions
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
