using System;
using System.Collections.Generic;
using System.Text;

namespace Akka.CQRS.Events
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a "buy"-side event
    /// </summary>
    public sealed class Bid : IWithStockId, IWithOrderId
    {
        public Bid(string stockId, string orderId, decimal bidPrice, 
            double bidQuantity, DateTimeOffset timeIssued)
        {
            StockId = stockId;
            BidPrice = bidPrice;
            BidQuantity = bidQuantity;
            TimeIssued = timeIssued;
            OrderId = orderId;
        }

        public string StockId { get; }

        public decimal BidPrice { get; }

        public double BidQuantity { get; }

        public DateTimeOffset TimeIssued { get; }
        public string OrderId { get; }

        private bool Equals(Bid other)
        {
            return string.Equals(StockId, other.StockId) && BidPrice == other.BidPrice && BidQuantity.Equals(other.BidQuantity) && string.Equals(OrderId, other.OrderId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Bid other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StockId.GetHashCode();
                hashCode = (hashCode * 397) ^ BidPrice.GetHashCode();
                hashCode = (hashCode * 397) ^ BidQuantity.GetHashCode();
                hashCode = (hashCode * 397) ^ OrderId.GetHashCode();
                return hashCode;
            }
        }
    }
}
