using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.CQRS.Pricing.Subscriptions.Actor
{
    /// <summary>
    /// Implements subscriptions without any of the DistributedPubSub built-in messaging types.
    /// </summary>
    public sealed class ActorMarketEventSubscriptionManager : MarketEventSubscriptionManagerBase
    {
        private readonly IActorRef _targetActor;

        public ActorMarketEventSubscriptionManager(IActorRef targetActor)
        {
            _targetActor = targetActor;
        }

        public override async Task<MarketSubscribeAck> Subscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            return await _targetActor.Ask<MarketSubscribeAck>(new MarketSubscribe(tickerSymbol, events, subscriber),
                TimeSpan.FromSeconds(3));
        }

        public override async Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber)
        {
            return await _targetActor.Ask<MarketUnsubscribeAck>(new MarketUnsubscribe(tickerSymbol, events,
                subscriber), TimeSpan.FromSeconds(3));
        }
    }
}
