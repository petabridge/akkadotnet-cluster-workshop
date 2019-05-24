using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.CQRS.Events;
using Akka.CQRS.Subscriptions.Actor;
using Akka.CQRS.TradeProcessor.Actors;
using Akka.Persistence.Extras;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.CQRS.Subscriptions.Tests.Actor
{
    public class ActorTradeSubscriptionManagerEnd2EndSpecs : TestKit.Xunit2.TestKit
    {
        public ActorTradeSubscriptionManagerEnd2EndSpecs(ITestOutputHelper output)
            : base(output: output)
        {
            _orderBookMaster = Sys.ActorOf(Props.Create(() => new OrderBookMasterActor()), "orders");
        }

        private IActorRef _orderBookMaster;

        [Fact(DisplayName =
            "[ActorTradeSubscriptionManager] Should be able to subscribe and publish to trade event topics.")]
        public async Task ShouldSubscribeAndPublishToTradeEventTopics()
        {
            var subManager = new ActorTradeSubscriptionManager(_orderBookMaster);


            // Subscribe to all topics
            var subAck = await subManager.Subscribe("MSFT", TestActor);
            subAck.StockId.Should().Be("MSFT");
            ExpectMsg<TradeSubscribeAck>(); // message should be sent back to us as well

            // create a matching trade, which should result in a Fill + Match being published.
            var time = DateTimeOffset.UtcNow;
            var bid = new Bid("MSFT", "foo1", 10.0m, 1.0d, time);
            var ask = new Ask("MSFT", "foo2", 10.0m, 1.0d, time);
            _orderBookMaster.Tell(bid);
            _orderBookMaster.Tell(ask);

            ExpectMsgAllOf<IWithStockId>(new Fill("foo1", "MSFT", 1.0d, 10.0m, "foo2", time),
                new Fill("foo2", "MSFT", 1.0d, 10.0m, "foo1", time),
                new Match("MSFT", "foo2", "foo1", 10.0m, 1.0d, time));
        }
    }
}
