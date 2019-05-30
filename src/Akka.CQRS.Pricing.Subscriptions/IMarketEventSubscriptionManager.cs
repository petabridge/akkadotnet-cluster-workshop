// -----------------------------------------------------------------------
// <copyright file="ITradeEventSubscriptionManager.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.CQRS.Pricing.Subscriptions
{
    /// <summary>
    /// Abstraction used to manage subscriptions for <see cref="IMarketEvent"/>s.
    /// </summary>
    public interface IMarketEventSubscriptionManager
    {
        Task<MarketSubscribeAck> Subscribe(string tickerSymbol, IActorRef subscriber);

        Task<MarketSubscribeAck> Subscribe(string tickerSymbol, MarketEventType @event, IActorRef subscriber);

        Task<MarketSubscribeAck> Subscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber);

        Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, MarketEventType[] events, IActorRef subscriber);

        Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, MarketEventType @event, IActorRef subscriber);

        Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, IActorRef subscriber);
    }
}