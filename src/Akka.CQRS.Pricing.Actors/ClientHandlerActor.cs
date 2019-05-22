using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.CQRS.Pricing.Subscriptions.Client;
using Akka.CQRS.Subscriptions;

namespace Akka.CQRS.Pricing.Actors
{
    /// <summary>
    /// Responsible for handling inbound requests from the <see cref="Akka.Cluster.Tools.Client.ClusterClient"/>
    /// actors running on the Web nodes.
    /// </summary>
    public sealed class ClientHandlerActor : ReceiveActor
    {
        private readonly IActorRef _priceRouter;

        public ClientHandlerActor(IActorRef priceRouter)
        {
            _priceRouter = priceRouter;

            Receive<SubscribeClient>(s =>
            {
                _priceRouter.Tell(new TradeSubscribe(s.StockId, TradeEventHelpers.AllTradeEventTypes, Sender));
            });

            Receive<SubscribeClientAll>(a =>
            {
                foreach (var s in AvailableTickerSymbols.Symbols)
                {
                    _priceRouter.Tell(new TradeSubscribe(s, TradeEventHelpers.AllTradeEventTypes, Sender));
                }
            });

            Receive<UnsubscribeClient>(s =>
            {
                _priceRouter.Tell(new TradeUnsubscribe(s.StockId, TradeEventHelpers.AllTradeEventTypes, Sender));
            });
        }
    }
}
