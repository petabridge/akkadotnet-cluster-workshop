using System;
using System.Collections.Generic;
using System.Text;
using Akka.Routing;

namespace Akka.CQRS.Infrastructure
{
    /// <summary>
    /// Creates a <see cref="ConsistentHashMapping"/>
    /// </summary>
    public static class TradeEventConsistentHashMapping
    {
        public static readonly ConsistentHashMapping TradeEventMapping = msg =>
        {
            if (msg is IWithStockId s)
            {
                return s.StockId;
            }

            return msg.ToString();
        };
    }
}
