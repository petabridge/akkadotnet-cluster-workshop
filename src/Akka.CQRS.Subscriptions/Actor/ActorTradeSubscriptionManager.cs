using System;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.CQRS.Subscriptions.Actor
{
    /// <summary>
    /// Implements subscriptions without any of the DistributedPubSub built-in messaging types.
    /// </summary>
    public sealed class ActorTradeSubscriptionManager : TradeEventSubscriptionManagerBase
    {
        private readonly IActorRef _targetActor;

        public ActorTradeSubscriptionManager(IActorRef targetActor)
        {
            _targetActor = targetActor;
        }

        public override async Task<TradeSubscribeAck> Subscribe(string tickerSymbol, TradeEventType[] events, IActorRef subscriber)
        {
            return await _targetActor.Ask<TradeSubscribeAck>(new TradeSubscribe(tickerSymbol, events, subscriber), TimeSpan.FromSeconds(3));
        }

        public override async Task<TradeUnsubscribeAck> Unsubscribe(string tickerSymbol, TradeEventType[] events, IActorRef subscriber)
        {
            return await _targetActor.Ask<TradeUnsubscribeAck>(new TradeUnsubscribe(tickerSymbol, events, subscriber), TimeSpan.FromSeconds(3));
        }
    }
}
