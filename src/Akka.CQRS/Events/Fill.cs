using System;
using System.Collections.Generic;
using System.Text;

namespace Akka.CQRS.Events
{
    /// <summary>
    /// Fill an open order
    /// </summary>
    public sealed class Fill : IWithOrderId, IWithStockId, IEquatable<Fill>
    {
        public Fill(string orderId, string stockId, double quantity, decimal price,
            string filledById, DateTimeOffset timestamp, bool partialFill = false)
        {
            OrderId = orderId;
            Quantity = quantity;
            Price = price;
            FilledById = filledById;
            Timestamp = timestamp;
            StockId = stockId;
            Partial = partialFill;
        }

        public string OrderId { get; }

        public double Quantity { get; }

        public decimal Price { get; }

        public string FilledById { get; }

        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// When <c>true</c>, indicates that the order was only partially filled.
        /// </summary>
        public bool Partial { get; }

        public string StockId { get; }

        public bool Equals(Fill other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(OrderId, other.OrderId) && Quantity.Equals(other.Quantity) 
                                                         && Price == other.Price && string.Equals(FilledById, other.FilledById) && Partial == other.Partial && string.Equals(StockId, other.StockId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Fill other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = OrderId.GetHashCode();
                hashCode = (hashCode * 397) ^ Quantity.GetHashCode();
                hashCode = (hashCode * 397) ^ Price.GetHashCode();
                hashCode = (hashCode * 397) ^ FilledById.GetHashCode();
                hashCode = (hashCode * 397) ^ Partial.GetHashCode();
                hashCode = (hashCode * 397) ^ StockId.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(Fill left, Fill right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Fill left, Fill right)
        {
            return !Equals(left, right);
        }
    }
}
