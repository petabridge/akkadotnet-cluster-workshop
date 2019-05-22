using System;
using System.Collections.Generic;
using System.Text;

namespace Akka.CQRS.Pricing
{
    /// <summary>
    /// Marker interface for "market" events - i.e. changes in the market's view
    /// of price, volume, or other "aggregated" events not specific to any individual
    /// trade or order.
    /// </summary>
    public interface IMarketEvent : IWithStockId
    {
    }
}
