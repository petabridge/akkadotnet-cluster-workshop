using System;
using System.Linq;
using Akka.Actor;
using Akka.CQRS.Commands;
using Akka.CQRS.Events;
using Akka.CQRS.Matching;
using Akka.CQRS.Subscriptions;
using Akka.CQRS.Subscriptions.DistributedPubSub;
using Akka.CQRS.Subscriptions.InMem;
using Akka.CQRS.Subscriptions.NoOp;
using Akka.Event;
using Akka.Persistence;
using Akka.Persistence.Extras;

namespace Akka.CQRS.TradeProcessor.Actors
{
    /// <summary>
    /// Actor responsible for processing orders for a specific ticker symbol.
    /// </summary>
    public class OrderBookActor : ReceivePersistentActor
    {
        public static Props PropsFor(string tickerSymbol)
        {
            return Props.Create(() => new OrderBookActor(tickerSymbol));
        }

        /// <summary>
        /// Take a snapshot every N messages persisted.
        /// </summary>
        public const int SnapshotInterval = 100;
        private MatchingEngine _matchingEngine;
        private readonly ITradeEventPublisher _publisher;
        private readonly ITradeEventSubscriptionManager _subscriptionManager;
        private readonly IActorRef _confirmationActor;

        private readonly ILoggingAdapter _log = Context.GetLogger();

        /// <summary>
        /// Used when running under the <see cref="OrderBookMasterActor"/>
        /// </summary>
        /// <param name="tickerSymbol">The stock ticker symbol.</param>
        /// <param name="subscriptions">An in-memory trade event publisher / subscription manager.</param>
        public OrderBookActor(string tickerSymbol, InMemoryTradeEventPublisher subscriptions) : this(tickerSymbol, null,
            subscriptions, subscriptions, Context.Parent)
        {
        }

        public OrderBookActor(string tickerSymbol) : this(tickerSymbol, null, DistributedPubSubTradeEventPublisher.For(Context.System), NoOpTradeEventSubscriptionManager.Instance, Context.Parent) { }
        public OrderBookActor(string tickerSymbol, IActorRef confirmationActor) : this(tickerSymbol, null, DistributedPubSubTradeEventPublisher.For(Context.System), NoOpTradeEventSubscriptionManager.Instance, confirmationActor) { }
        public OrderBookActor(string tickerSymbol, MatchingEngine matchingEngine, ITradeEventPublisher publisher, ITradeEventSubscriptionManager subscriptionManager, IActorRef confirmationActor)
        {
            TickerSymbol = tickerSymbol;
            PersistenceId = $"{TickerSymbol}-orderBook";
            _matchingEngine = matchingEngine ?? CreateDefaultMatchingEngine(tickerSymbol, _log);
            _publisher = publisher;
            _confirmationActor = confirmationActor;
            _subscriptionManager = subscriptionManager;

            Recovers();
            Commands();
        }

        private static MatchingEngine CreateDefaultMatchingEngine(string tickerSymbol, ILoggingAdapter logger)
        {
            return new MatchingEngine(tickerSymbol, logger);
        }

        public string TickerSymbol { get; }
        public override string PersistenceId { get; }

        private void Recovers()
        {
            Recover<SnapshotOffer>(offer =>
            {
                if (offer.Snapshot is OrderbookSnapshot orderBook)
                {
                    _matchingEngine = MatchingEngine.FromSnapshot(orderBook, _log);
                }
            });

            Recover<Bid>(b => { _matchingEngine.WithBid(b); });
            Recover<Ask>(a => { _matchingEngine.WithAsk(a); });

            // Fill and Match can't modify the state of the MatchingEngine.
            Recover<Match>(m => { });
            Recover<Fill>(f => { });
        }

        private void Commands()
        {
            Command<ConfirmableMessage<Ask>>(a =>
            {
                // For the sake of efficiency - update orderbook and then persist all events
                var confirmation = new Confirmation(a.ConfirmationId, PersistenceId);
                var ask = a.Message;
                ProcessAsk(ask, confirmation);
            });

            Command<Ask>(a =>
            {
                ProcessAsk(a, null);
            });

            Command<ConfirmableMessage<Bid>>(b =>
            {
                // For the sake of efficiency -update orderbook and then persist all events
                var confirmation = new Confirmation(b.ConfirmationId, PersistenceId);
                var bid = b.Message;
                ProcessBid(bid, confirmation);
            });

            Command<Bid>(b =>
            {
                ProcessBid(b, null);
            });

            /*
             * Handle subscriptions directly in case we're using in-memory, local pub-sub.
             */
            CommandAsync<TradeSubscribe>(async sub =>
                {
                    try
                    {
                        var ack = await _subscriptionManager.Subscribe(sub.StockId, sub.Events, sub.Subscriber);
                        Context.Watch(sub.Subscriber);
                        sub.Subscriber.Tell(ack);
                        Sender.Tell(ack); // need this for ASK operations.
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Error while processing subscription {0}", sub);
                        sub.Subscriber.Tell(new TradeSubscribeNack(sub.StockId, sub.Events, ex.Message));
                    }
                });

            CommandAsync<TradeUnsubscribe>(async unsub =>
            {
                try
                {
                    var ack = await _subscriptionManager.Unsubscribe(unsub.StockId, unsub.Events, unsub.Subscriber);
                    // leave DeathWatch intact, in case actor is still subscribed to additional topics
                    unsub.Subscriber.Tell(ack);
                    Sender.Tell(ack); // need this for ASK operations.
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error while processing unsubscribe {0}", unsub);
                    unsub.Subscriber.Tell(new TradeUnsubscribeNack(unsub.StockId, unsub.Events, ex.Message));
                }
            });

            CommandAsync<Terminated>(async t =>
            {
                try
                {
                    var ack = await _subscriptionManager.Unsubscribe(TickerSymbol, t.ActorRef);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error while processing unsubscribe for terminated subscriber {0} for symbol {1}", t.ActorRef, TickerSymbol);
                }
            });

            Command<SaveSnapshotSuccess>(s =>
            {
                // clean-up prior snapshots and journal events
                DeleteSnapshots(new SnapshotSelectionCriteria(s.Metadata.SequenceNr - 1));
                DeleteMessages(s.Metadata.SequenceNr);
            });

            Command<GetOrderBookSnapshot>(s =>
            {
                Sender.Tell(_matchingEngine.GetSnapshot());
            });
        }

        private void ProcessBid(Bid bid, Confirmation confirmation)
        {
            var events = _matchingEngine.WithBid(bid);
            var persistableEvents = new ITradeEvent[] {bid}.Concat<ITradeEvent>(events); // bid needs to go before Fill / Match

            _log.Info("[{0}][{1}] - {2} units @ {3} per unit", TickerSymbol, bid.ToTradeEventType(), bid.BidQuantity,
                bid.BidPrice);

            PersistAll(persistableEvents, @event => { PersistTrade(@event, confirmation); });
        }

        private void ProcessAsk(Ask ask, Confirmation confirmation)
        {
            var events = _matchingEngine.WithAsk(ask);
            var persistableEvents = new ITradeEvent[] {ask}.Concat<ITradeEvent>(events); // ask needs to go before Fill / Match


            _log.Info("[{0}][{1}] - {2} units @ {3} per unit", TickerSymbol, ask.ToTradeEventType(), ask.AskQuantity,
                ask.AskPrice);

            PersistAll(persistableEvents, @event => { PersistTrade(@event, confirmation); });
        }

        private void PersistTrade(ITradeEvent @event, Confirmation confirmation)
        {
            if (confirmation != null && (@event is Ask || @event is Bid))
            {
                // need to use the ID of the original sender to satisfy the PersistenceSupervisor
                Sender.Tell(confirmation);
            }

            _publisher.Publish(TickerSymbol, @event);

            // Take a snapshot every N messages to optimize recovery time
            if (LastSequenceNr % SnapshotInterval == 0)
            {
                SaveSnapshot(_matchingEngine.GetSnapshot());
            }
        }
    }
}
