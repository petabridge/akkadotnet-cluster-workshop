using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.CQRS.Subscriptions.NoOp
{
    /// <summary>
    /// Used to ignore subscription management events.
    /// </summary>
    public sealed class NoOpTradeEventSubscriptionManager : TradeEventSubscriptionManagerBase
    {
        public static readonly NoOpTradeEventSubscriptionManager Instance = new NoOpTradeEventSubscriptionManager();
        private NoOpTradeEventSubscriptionManager() { }

        public override Task<TradeSubscribeAck> Subscribe(string tickerSymbol, TradeEventType[] events, IActorRef subscriber)
        {
            return Task.FromResult(new TradeSubscribeAck(tickerSymbol, TradeEventHelpers.AllTradeEventTypes));
        }

        public override Task<TradeUnsubscribeAck> Unsubscribe(string tickerSymbol, TradeEventType[] events, IActorRef subscriber)
        {
            return Task.FromResult(new TradeUnsubscribeAck(tickerSymbol, events));
        }

    }
}
