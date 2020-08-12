using System;
using System.Linq;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.CQRS.Events;
using Akka.CQRS.Pricing.Commands;
using Akka.CQRS.Pricing.Events;
using Akka.CQRS.Pricing.Subscriptions;
using Akka.CQRS.Pricing.Subscriptions.DistributedPubSub;
using Akka.CQRS.Pricing.Views;
using Akka.CQRS.Subscriptions;
using Akka.CQRS.Subscriptions.DistributedPubSub;
using Akka.CQRS.Util;
using Akka.Event;
using Akka.Persistence;
using Petabridge.Collections;

namespace Akka.CQRS.Pricing.Actors
{
    /// <summary>
    /// Used to aggregate <see cref="Akka.CQRS.Events.Match"/> events via Akka.Persistence.Query
    /// </summary>
    public class MatchAggregator : ReceivePersistentActor
    {
        // Take a snapshot every 10 journal entries
        public const int SnapshotEveryN = 10; 

        private MatchAggregate _matchAggregate;
        private readonly ITimestamper _timestamper;
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly IMarketEventPublisher _marketEventPublisher;
        private readonly IMarketEventSubscriptionManager _marketEventSubscriptionManager;
        private readonly ITradeEventSubscriptionManager _tradeSubscriptionManager;
        private CircularBuffer<IPriceUpdate> _priceUpdates = new CircularBuffer<IPriceUpdate>(MatchAggregate.DefaultSampleSize);
        private CircularBuffer<IVolumeUpdate> _volumeUpdates = new CircularBuffer<IVolumeUpdate>(MatchAggregate.DefaultSampleSize);
        private ICancelable _publishPricesTask;

        public readonly string TickerSymbol;
        public override string PersistenceId { get; }

        private class PublishEvents
        {
            public static readonly PublishEvents Instance = new PublishEvents();
            private PublishEvents() { }
        }

        private class DoSubscribe
        {
            public static readonly DoSubscribe Instance = new DoSubscribe();
            private DoSubscribe() { }
        }

        public MatchAggregator(string tickerSymbol) : this(tickerSymbol, DistributedPubSubTradeEventSubscriptionManager.For(Context.System), 
            DistributedPubSubMarketEventSubscriptionManager.For(Context.System), DistributedPubSubMarketEventPublisher.For(Context.System))
        { }

        public MatchAggregator(string tickerSymbol, ITradeEventSubscriptionManager tradeSubscriptionManager, IMarketEventSubscriptionManager marketEventSubscriptionManager, IMarketEventPublisher marketEventPublisher, ITimestamper timestamper = null)
        {
            TickerSymbol = tickerSymbol;

            _timestamper = timestamper ?? CurrentUtcTimestamper.Instance;
            _tradeSubscriptionManager = tradeSubscriptionManager;
            _marketEventSubscriptionManager = marketEventSubscriptionManager;
            _marketEventPublisher = marketEventPublisher;
            PersistenceId = EntityIdHelper.IdForPricing(tickerSymbol);
            
            Recovers();
            Commands();
        }

        private void Recovers()
        {
            /*
             * Can be saved as a snapshot or as an event
             */
            Recover<SnapshotOffer>(o =>
            {
                if (o.Snapshot is MatchAggregatorSnapshot s)
                {
                    RecoverAggregateData(s);
                }
            });

            Recover<MatchAggregatorSnapshot>(s => { RecoverAggregateData(s); });
        }

        /// <summary>
        /// Recovery has completed successfully.
        /// </summary>
        protected override void OnReplaySuccess()
        {         

            // setup subscription to TradeEventPublisher
            Self.Tell(DoSubscribe.Instance);

            base.OnReplaySuccess();
        }

        private void RecoverAggregateData(MatchAggregatorSnapshot s)
        {
            _matchAggregate = new MatchAggregate(TickerSymbol, s.RecentAvgPrice, s.RecentAvgVolume);

            if(s.RecentPriceUpdates != null && s.RecentPriceUpdates.Count > 0)
                _priceUpdates.Enqueue(s.RecentPriceUpdates.ToArray());

            if(s.RecentVolumeUpdates != null && s.RecentVolumeUpdates.Count > 0)
            _volumeUpdates.Enqueue(s.RecentVolumeUpdates.ToArray());
        }

        private MatchAggregatorSnapshot SaveAggregateData()
        {
            return new MatchAggregatorSnapshot(_matchAggregate.AvgPrice.CurrentAvg, _matchAggregate.AvgVolume.CurrentAvg, _priceUpdates.Cast<PriceChanged>().ToList(), _volumeUpdates.Cast<VolumeChanged>().ToList());
        }

        private void AwaitingSubscription()
        {
            CommandAsync<DoSubscribe>(async _ =>
            {
                try
                {
                    var ack = await _tradeSubscriptionManager.Subscribe(TickerSymbol, TradeEventType.Match, Self);
                    _log.Info("Successfully subscribed to MATCH events for {0}", TickerSymbol);
                    //Become(Commands);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error while waiting for SubscribeAck for [{0}-{1}] - retrying in 5s.", TickerSymbol, TradeEventType.Match);
                    Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5), Self, DoSubscribe.Instance, ActorRefs.NoSender);
                }
            });
        }

        private void Commands()
        {
            AwaitingSubscription();
            Command<Match>(m => TickerSymbol.Equals(m.StockId), m =>
            {
                _log.Info("Received MATCH for {0} - price: {1} quantity: {2}", TickerSymbol, m.SettlementPrice, m.Quantity);
                if (_matchAggregate == null)
                {
                    _matchAggregate = new MatchAggregate(TickerSymbol, m.SettlementPrice, m.Quantity);
                    return;
                } 

                if (!_matchAggregate.WithMatch(m))
                {
                    // should never get to here
                    _log.Warning("Received Match for ticker symbol [{0}] - but we only accept symbols for [{1}]", m.StockId, TickerSymbol);
                }
            });

            // Matches for a different ticker symbol
            Command<Match>(m =>
            {
                _log.Warning("Received Match for ticker symbol [{0}] - but we only accept symbols for [{1}]", m.StockId, TickerSymbol);
            });

            // Command sent to pull down a complete snapshot of active pricing data for this ticker symbol
            Command<FetchPriceAndVolume>(f =>
            {
                // no price data yet
                if (_priceUpdates.Count == 0 || _volumeUpdates.Count == 0)
                {
                    Sender.Tell(PriceAndVolumeSnapshot.Empty(TickerSymbol));
                }
                else
                {
                    Sender.Tell(new PriceAndVolumeSnapshot(TickerSymbol, _priceUpdates.ToArray(), _volumeUpdates.ToArray()));
                }
                
            });

            Command<PublishEvents>(p =>
            {
                if (_matchAggregate == null)
                    return;

                var (latestPrice, latestVolume) = _matchAggregate.FetchMetrics(_timestamper);

                // Need to update pricing records prior to persisting our state, since this data is included in
                // output of SaveAggregateData()
                _priceUpdates.Add(latestPrice);
                _volumeUpdates.Add(latestVolume);

                PersistAsync(SaveAggregateData(), snapshot =>
                {
                    _log.Info("Saved latest price {0} and volume {1}", snapshot.RecentAvgPrice, snapshot.RecentAvgVolume);
                    if (LastSequenceNr % SnapshotEveryN == 0)
                    {
                        SaveSnapshot(snapshot);
                    }
                });

                // publish updates to price and volume subscribers
                _marketEventPublisher.Publish(TickerSymbol, latestPrice);
                _marketEventPublisher.Publish(TickerSymbol, latestVolume);
            });

            Command<Ping>(p =>
            {
                if (_log.IsDebugEnabled)
                {
                    _log.Debug("pinged via {0}", Sender);
                }
            });

            Command<SaveSnapshotSuccess>(s =>
            {
                // clean-up prior snapshots and journal events
                DeleteSnapshots(new SnapshotSelectionCriteria(s.Metadata.SequenceNr-1));
                DeleteMessages(s.Metadata.SequenceNr);
            });

            /*
             * Handle subscriptions directly in case we're using in-memory, local pub-sub.
             */
            CommandAsync<MarketSubscribe>(async sub =>
            {
                try
                {
                    var ack = await _marketEventSubscriptionManager.Subscribe(TickerSymbol, sub.Events, sub.Subscriber);
                    Context.Watch(sub.Subscriber);
                    sub.Subscriber.Tell(ack);
                    Sender.Tell(ack); // need this for ASK operations.
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error while processing subscription {0}", sub);
                    sub.Subscriber.Tell(new MarketSubscribeNack(TickerSymbol, sub.Events, ex.Message));
                }
            });

            CommandAsync<MarketUnsubscribe>(async unsub =>
            {
                try
                {
                    var ack = await _marketEventSubscriptionManager.Unsubscribe(PersistenceId, unsub.Events, unsub.Subscriber);
                    // leave DeathWatch intact, in case actor is still subscribed to additional topics
                    unsub.Subscriber.Tell(ack);
                    Sender.Tell(ack); // need this for ASK operations.
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error while processing unsubscribe {0}", unsub);
                    unsub.Subscriber.Tell(new MarketUnsubscribeNack(TickerSymbol, unsub.Events, ex.Message));
                }
            });

            CommandAsync<Terminated>(async t =>
            {
                try
                {
                    var ack = await _marketEventSubscriptionManager.Unsubscribe(TickerSymbol, t.ActorRef);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error while processing unsubscribe for terminated subscriber {0} for symbol {1}", t.ActorRef, TickerSymbol);
                }
            });
        }

        protected override void PreStart()
        {
            _publishPricesTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromSeconds(10),
               TimeSpan.FromSeconds(10), Self, PublishEvents.Instance, ActorRefs.NoSender);

            _log.Info("Starting...");
            base.PreStart();
        }

        protected override void PostStop()
        {
            _publishPricesTask?.Cancel();
            base.PostStop();
        }
    }
}
