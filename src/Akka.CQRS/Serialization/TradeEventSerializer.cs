using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Akka.Actor;
using Akka.CQRS.Commands;
using Akka.CQRS.Events;
using Akka.Serialization;

namespace Akka.CQRS.Serialization
{
    public sealed class TradeEventSerializer : SerializerWithStringManifest
    {
        public TradeEventSerializer(ExtendedActorSystem system) : base(system)
        {
        }

        public override byte[] ToBinary(object obj)
        {
            throw new NotImplementedException();
        }

        public override object FromBinary(byte[] bytes, string manifest)
        {
            throw new NotImplementedException();
        }

        public override string Manifest(object o)
        {
            switch (o)
            {
                case Order ord:
                    return "O";
                case Ask a:
                    return "A";
                case Bid b:
                    return "B";
                case Fill f:
                    return "F";
                case Match m:
                    return "m";
                case OrderbookSnapshot snap:
                    return "OBS";
                case GetOrderBookSnapshot go:
                    return "GOBS";
                case GetRecentMatches grm:
                    return "GRM";
                default:
                    throw new SerializationException($"Type {o.GetType()} is not supported by this serializer.");
            }
        }

        internal static Order FromProto(Msgs.Order o)
        {
            if (o.Fills.Count > 0)
            {
                return new Order(o.OrderId, o.StockId,);
            }
        }

        internal static TradeSide FromProto(Msgs.TradeSide t)
        {
            switch (t)
            {
                case Msgs.TradeSide.Buy:
                    return TradeSide.Buy;
                case Msgs.TradeSide.Sell:
                    return TradeSide.Sell;
                default:
                    throw new SerializationException($"Unrecognized TradeSide [{t}]");
            }
        }

        internal static Msgs.TradeSide ToProto(TradeSide t)
        {
            switch (t)
            {
                case TradeSide.Buy:
                    return Msgs.TradeSide.Buy;
                case TradeSide.Sell:
                    return Msgs.TradeSide.Sell;
                default:
                    throw new SerializationException($"Unrecognized TradeSide [{t}]");
            }
        }

        internal static Fill FromProto(Msgs.Fill f)
        {
            return new Fill(f.OrderId, f.StockId, f.Quantity, (decimal)f.Price, f.FilledById,
                DateTimeOffset.FromUnixTimeMilliseconds(f.TimeIssued));
        }
    }
}
