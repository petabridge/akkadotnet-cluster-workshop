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

    /// <summary>
    /// Abstract base class for <see cref="IMarketEventSubscriptionManager"/> implementations.
    /// </summary>
    public abstract class MarketEventSubscriptionManagerBase : IMarketEventSubscriptionManager
    {
        public async Task<MarketSubscribeAck> Subscribe(string tickerSymbol, IActorRef subscriber)
        {
            return await Subscribe(tickerSymbol, MarketEventHelpers.AllTradeEventTypes, subscriber);
        }

        public async Task<MarketSubscribeAck> Subscribe(string tickerSymbol, MarketEventType @event, IActorRef subscriber)
        {
            return await Subscribe(tickerSymbol, new []{ @event }, subscriber);
        }

        public abstract Task<MarketSubscribeAck> Subscribe(string tickerSymbol, MarketEventType[] events,
            IActorRef subscriber);

        public abstract Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, MarketEventType[] events,
            IActorRef subscriber);

        public async Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, MarketEventType @event, IActorRef subscriber)
        {
            return await Unsubscribe(tickerSymbol, new[] {@event}, subscriber);
        }

        public async Task<MarketUnsubscribeAck> Unsubscribe(string tickerSymbol, IActorRef subscriber)
        {
            return await Unsubscribe(tickerSymbol, MarketEventHelpers.AllTradeEventTypes, subscriber);
        }
    }
}