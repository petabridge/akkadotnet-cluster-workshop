using System;
using System.Collections.Generic;
using System.Text;
using Akka.CQRS.Commands;
using Akka.CQRS.Events;
using Akka.CQRS.Serialization;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.CQRS.Tests.Serialization
{
    public class TradeEventSerializerSpecs : TestKit.Xunit2.TestKit
    {
        public TradeEventSerializerSpecs(ITestOutputHelper output) 
            : base(TradeEventSerializer.Config, output: output) { }

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
    }
}
