using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Akka.Actor;
using Akka.Configuration;
using Akka.CQRS.Commands;
using Akka.CQRS.Events;
using Akka.Serialization;
using Google.Protobuf;

namespace Akka.CQRS.Serialization
{
    public sealed class TradeEventSerializer : SerializerWithStringManifest
    {
        public static readonly IEnumerable<Order> EmptyOrders = new Order[0];

        public static Config Config { get; }

        static TradeEventSerializer()
        {
            Config = ConfigurationFactory.FromResource<TradeEventSerializer>(
                "Akka.CQRS.Serialization.TradeEventSerializer.conf");
        }

        public TradeEventSerializer(ExtendedActorSystem system) : base(system)
        {
        }

        public override int Identifier => 517;

        public override byte[] ToBinary(object obj)
        {
            switch (obj)
            {
                case Order ord:
                    return ToProto(ord).ToByteArray();
                case Ask a:
                    return ToProto(a).ToByteArray();
                case Bid b:
                    return ToProto(b).ToByteArray();
                case Fill f:
                    return ToProto(f).ToByteArray();
                case Match m:
                    return ToProto(m).ToByteArray();
                case OrderbookSnapshot snap:
                    return ToProto(snap).ToByteArray();
                case GetOrderBookSnapshot go:
                    return ToProto(go).ToByteArray();
                case GetRecentMatches grm:
                    return ToProto(grm).ToByteArray();
                default:
                    throw new SerializationException($"Type {obj.GetType()} is not supported by this serializer.");
            }
        }

        public override object FromBinary(byte[] bytes, string manifest)
        {
            switch (manifest)
            {
                case "O":
                    return FromProto(Msgs.Order.Parser.ParseFrom(bytes));
                case "A":
                    return FromProto(Msgs.Ask.Parser.ParseFrom(bytes));
                case "B":
                    return FromProto(Msgs.Bid.Parser.ParseFrom(bytes));
                case "F":
                    return FromProto(Msgs.Fill.Parser.ParseFrom(bytes));
                case "M":
                    return FromProto(Msgs.Match.Parser.ParseFrom(bytes));
                case "OBS":
                    return FromProto(Msgs.OrderbookSnapshot.Parser.ParseFrom(bytes));
                case "GOBS":
                    return FromProto(Msgs.GetOrderbookSnapshot.Parser.ParseFrom(bytes));
                case "GRM":
                    return FromProto(Msgs.GetRecentMatches.Parser.ParseFrom(bytes));
                default:
                    throw new SerializationException($"Type {manifest} is not supported by this serializer.");
            }
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
                    return "M";
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

        internal static Msgs.GetRecentMatches ToProto(GetRecentMatches g)
        {
            return new Msgs.GetRecentMatches(){ StockId = g.StockId};
        }

        internal static GetRecentMatches FromProto(Msgs.GetRecentMatches g)
        {
            return new GetRecentMatches(g.StockId);
        }

        internal static Msgs.GetOrderbookSnapshot ToProto(GetOrderBookSnapshot g)
        {
            return new Msgs.GetOrderbookSnapshot()
            {
                StockId = g.StockId
            };
        }

        internal static GetOrderBookSnapshot FromProto(Msgs.GetOrderbookSnapshot g)
        {
            return new GetOrderBookSnapshot(g.StockId);
        }

        internal static Msgs.OrderbookSnapshot ToProto(OrderbookSnapshot o)
        {
            var obs = new Msgs.OrderbookSnapshot()
            {
                AskQuantity = o.AskQuantity,
                BidQuantity = o.BidQuantity,
                StockId = o.StockId,
                TimeIssued = o.Timestamp.ToUnixTimeMilliseconds()
            };

            if (o.Asks.Count > 0)
            {
                obs.Asks.AddRange(o.Asks.Select(x => ToProto(x)));
            }

            if (o.Bids.Count > 0)
            {
                obs.Bids.AddRange(o.Bids.Select(x => ToProto(x)));
            }

            return obs;
        }

        internal static OrderbookSnapshot FromProto(Msgs.OrderbookSnapshot o)
        {
            var asks = (o.Asks.Count > 0 ? o.Asks.Select(x => FromProto(x)) : EmptyOrders);
            var bids = o.Bids.Count > 0 ? o.Bids.Select(x => FromProto(x)) : EmptyOrders;

            return new OrderbookSnapshot(o.StockId, DateTimeOffset.FromUnixTimeMilliseconds(o.TimeIssued), o.AskQuantity, o.BidQuantity, asks.ToList(), bids.ToList());
        }


        internal static Msgs.Match ToProto(Match m)
        {
            return new Msgs.Match()
            {
                StockId = m.StockId,
                BuyOrderId = m.BuyOrderId,
                SellOrderId = m.SellOrderId,
                Price = (double)m.SettlementPrice,
                TimeIssued = m.TimeStamp.ToUnixTimeMilliseconds(),
                Quantity = m.Quantity
            };
        }

        internal static Match FromProto(Msgs.Match m)
        {
            return new Match(m.StockId, m.BuyOrderId, m.SellOrderId, (decimal) m.Price, m.Quantity,
                DateTimeOffset.FromUnixTimeMilliseconds(m.TimeIssued));
        }

        internal static Msgs.Ask ToProto(Ask ask)
        {
            return new Msgs.Ask()
            {
                OrderId = ask.OrderId,
                StockId = ask.StockId,
                Price = (double)ask.AskPrice,
                Quantity = ask.AskQuantity,
                TimeIssued = ask.TimeIssued.ToUnixTimeMilliseconds()
            };
        }

        internal static Bid FromProto(Msgs.Bid b)
        {
            return new Bid(b.StockId, b.OrderId, (decimal) b.Price, b.Quantity,
                DateTimeOffset.FromUnixTimeMilliseconds(b.TimeIssued));
        }

        internal static Msgs.Bid ToProto(Bid ask)
        {
            return new Msgs.Bid()
            {
                OrderId = ask.OrderId,
                StockId = ask.StockId,
                Price = (double)ask.BidPrice,
                Quantity = ask.BidQuantity,
                TimeIssued = ask.TimeIssued.ToUnixTimeMilliseconds()
            };
        }

        internal static Ask FromProto(Msgs.Ask a)
        {
            return new Ask(a.StockId, a.OrderId, (decimal)a.Price, a.Quantity,
                DateTimeOffset.FromUnixTimeMilliseconds(a.TimeIssued));
        }

        internal static Order FromProto(Msgs.Order o)
        {
            if (o.Fills.Count > 0)
            {
                return new Order(o.OrderId, o.StockId, FromProto(o.Side), o.Quantity, (decimal)o.Price, 
                    DateTimeOffset.FromUnixTimeMilliseconds(o.TimeIssued), o.Fills.Select(x => FromProto(x)).ToImmutableList());
            }

            return new Order(o.OrderId, o.StockId, FromProto(o.Side), o.Quantity, (decimal)o.Price,
                DateTimeOffset.FromUnixTimeMilliseconds(o.TimeIssued));
        }

        internal static Msgs.Order ToProto(Order o)
        {
            var pOrder = new Msgs.Order(){ OrderId = o.OrderId, StockId = o.StockId, Price = (double)o.Price,
                Quantity = o.OriginalQuantity, Side = ToProto(o.Side), TimeIssued = o.TimeIssued.ToUnixTimeMilliseconds()};

            if (o.Fills.Count > 0)
            {
                pOrder.Fills.AddRange(o.Fills.Select(x => ToProto(x)));
            }

            return pOrder;
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
                DateTimeOffset.FromUnixTimeMilliseconds(f.TimeIssued), f.PartialFill);
        }

        internal static Msgs.Fill ToProto(Fill f)
        {
            return new Msgs.Fill() { FilledById = f.FilledById, OrderId = f.OrderId, PartialFill = f.Partial,
                Price = (double)f.Price, Quantity = f.Quantity, StockId = f.StockId, TimeIssued = f.Timestamp.ToUnixTimeMilliseconds() };
        }
    }
}
