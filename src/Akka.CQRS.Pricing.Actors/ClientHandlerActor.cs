using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.CQRS.Pricing.Subscriptions;
using Akka.CQRS.Pricing.Subscriptions.Client;
using Akka.CQRS.Subscriptions;
using Akka.Event;

namespace Akka.CQRS.Pricing.Actors
{
    /// <summary>
    /// Responsible for handling inbound requests from the <see cref="Akka.Cluster.Tools.Client.ClusterClient"/>
    /// actors running on the Web nodes.
    /// </summary>
    public sealed class ClientHandlerActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly IActorRef _priceRouter;

        public ClientHandlerActor(IActorRef priceRouter)
        {
            _priceRouter = priceRouter;

            Receive<SubscribeClient>(s =>
            {
                _log.Info("Received {0} from {1}", s, Sender);
                _priceRouter.Tell(new MarketSubscribe(s.StockId, MarketEventHelpers.AllMarketEventTypes, Sender));
            });

            Receive<SubscribeClientAll>(a =>
            {
                _log.Info("Received {0} from {1}", a, Sender);
                foreach (var s in AvailableTickerSymbols.Symbols)
                {
                    _priceRouter.Tell(new MarketSubscribe(s, MarketEventHelpers.AllMarketEventTypes, Sender));
                }
            });

            Receive<UnsubscribeClient>(s =>
            {
                _log.Info("Received {0} from {1}", s, Sender);
                _priceRouter.Tell(new MarketUnsubscribe(s.StockId, MarketEventHelpers.AllMarketEventTypes, Sender));
            });
        }
    }
}
