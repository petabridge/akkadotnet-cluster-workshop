// -----------------------------------------------------------------------
// <copyright file="ITradeEventPublisher.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.CQRS.Pricing;

namespace Akka.CQRS.Subscriptions
{
    /// <summary>
    /// Abstraction used for publishing data about <see cref="ITradeEvent"/> instances.
    /// </summary>
    public interface ITradeEventPublisher
    {
        void Publish(string tickerSymbol, ITradeEvent @event);
    }

    /// <summary>
    /// Abstraction for publishing data about <see cref="IMarketEvent"/> instances.
    /// </summary>
    public interface IMarketEventPublisher
    {
        void Publish(string tickerSymbol, ITradeEvent @event);
    }
}