using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Akka.CQRS.Commands;
using Akka.CQRS.Events;
using Akka.CQRS.Serialization;
using Akka.CQRS.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.CQRS.Tests.Serialization
{
    public class TradeEventSerializerSpecs : TestKit.Xunit2.TestKit
    {
        public TradeEventSerializerSpecs(ITestOutputHelper output) 
            : base(TradeEventSerializer.Config, output: output) { }



        public static IEnumerable<object[]> GetSerializableObjects()
        {
            yield return new Match("fuber", "id", "id2", 10.0m, 10.0d, CurrentUtcTimestamper.Instance.Now).ToObjectArray();
            yield return new Fill("fuber2", "fub", 10.0d, 11.0001m, "fuber3", CurrentUtcTimestamper.Instance.Now).ToObjectArray();
            yield return new Fill("fuber2", "fub", 10.0d, 11.0001m, "fuber3", CurrentUtcTimestamper.Instance.Now, true).ToObjectArray();
            yield return new Ask("fuber1", "fub", 10.11m, 5.0d, CurrentUtcTimestamper.Instance.Now).ToObjectArray();
            yield return new Bid("fuber1", "fub", 10.11m, 5.0d, CurrentUtcTimestamper.Instance.Now).ToObjectArray();
            yield return new Order("fub", "stock1", TradeSide.Buy, 10.0d, 11.11m, CurrentUtcTimestamper.Instance.Now).ToObjectArray();
            yield return new Order("fub", "stock1", TradeSide.Sell, 10.0d, 11.11m, CurrentUtcTimestamper.Instance.Now).ToObjectArray();
            yield return new Order("fub", "stock1", TradeSide.Sell, 10.0d, 11.11m, CurrentUtcTimestamper.Instance.Now, ImmutableArray<Fill>.Empty.Add(new Fill("fuber2", "fub", 10.0d, 11.0001m, "fuber3", CurrentUtcTimestamper.Instance.Now))).ToObjectArray();
        }

        [Theory(DisplayName = "All ITradeEvents should be serializable via the TradeEventSerializer")]
        [InlineData(typeof(Match), 517)]
        [InlineData(typeof(Order), 517)]
        [InlineData(typeof(Fill), 517)]
        [InlineData(typeof(OrderbookSnapshot), 517)]
        [InlineData(typeof(Bid), 517)]
        [InlineData(typeof(Ask), 517)]
        [InlineData(typeof(GetRecentMatches), 517)]
        [InlineData(typeof(GetOrderBookSnapshot), 517)]
        public void ShouldUseTradeEventSerializer(Type type, int expectedSerializerId)
        {
            Sys.Serialization.FindSerializerForType(type).Identifier.Should().Be(expectedSerializerId);
        }

        [Theory]
        [MemberData(nameof(GetSerializableObjects))]
        public void ShouldSerializeTradeEvent(ITradeEvent trade)
        {
            VerifySerialization(trade);
        }

        private void VerifySerialization(ITradeEvent trade)
        {
            var serializer = Sys.Serialization.FindSerializerFor(trade).As<TradeEventSerializer>();
            var serialized = serializer.ToBinary(trade);
            var manifest = serializer.Manifest(trade);
            var deserialized = serializer.FromBinary(serialized, manifest);

            deserialized.Should().Be(trade);
        }
    }
}
