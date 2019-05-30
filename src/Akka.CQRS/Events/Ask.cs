using System;

namespace Akka.CQRS.Events
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a "sell"-side event
    /// </summary>
    public sealed class Ask : IWithStockId, IWithOrderId
    {
        public Ask(string stockId, string orderId, decimal askPrice, 
            double askQuantity, DateTimeOffset timeIssued)
        {
            StockId = stockId;
            AskPrice = askPrice;
            AskQuantity = askQuantity;
            TimeIssued = timeIssued;
            OrderId = orderId;
        }

        public string StockId { get; }

        public decimal AskPrice { get; }

        public double AskQuantity { get; }

        public DateTimeOffset TimeIssued { get; }
        public string OrderId { get; }

        private bool Equals(Ask other)
        {
            return string.Equals(StockId, other.StockId) && AskPrice == other.AskPrice && AskQuantity.Equals(other.AskQuantity) && string.Equals(OrderId, other.OrderId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Ask other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StockId.GetHashCode();
                hashCode = (hashCode * 397) ^ AskPrice.GetHashCode();
                hashCode = (hashCode * 397) ^ AskQuantity.GetHashCode();
                hashCode = (hashCode * 397) ^ OrderId.GetHashCode();
                return hashCode;
            }
        }
    }
}