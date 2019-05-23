using System;
using System.Collections.Generic;
using System.Text;

namespace Akka.CQRS.Commands
{
    /// <summary>
    /// Specifics the level of detail for an OrderBook snapshot.
    /// </summary>
    public enum DetailLevel
    {
        /// <summary>
        /// Lists all of the details
        /// </summary>
        Full,

        /// <summary>
        /// Lists only the aggregates
        /// </summary>
        Summary
    }

    /// <inheritdoc />
    /// <summary>
    /// Query the current order book snapshot
    /// </summary>
    public class GetOrderBookSnapshot : IWithStockId, ITradeEvent
    {
        public GetOrderBookSnapshot(string stockId)
        {
            StockId = stockId;
        }

        public string StockId { get; }
    }
}
