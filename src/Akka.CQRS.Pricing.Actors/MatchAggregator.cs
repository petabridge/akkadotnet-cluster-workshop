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
using Akka.Persistence.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
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
        private readonly IActorRef _mediator;
        private readonly ITimestamper _timestamper;
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly ITradeEventSubscriptionManager _subscriptionManager;
        private CircularBuffer<IPriceUpdate> _priceUpdates = new CircularBuffer<IPriceUpdate>(MatchAggregate.DefaultSampleSize);
        private CircularBuffer<IVolumeUpdate> _volumeUpdates = new CircularBuffer<IVolumeUpdate>(MatchAggregate.DefaultSampleSize);
        private ICancelable _publishPricesTask;

        private readonly string _priceTopic;
        private readonly string _volumeTopic;

        public readonly string TickerSymbol;
        public override string PersistenceId { get; }

        private class PublishEvents
        {
            public static readonly PublishEvents Instance = new PublishEvents();
            private PublishEvents() { }
        }

        public MatchAggregator(string tickerSymbol)
         : this(tickerSymbol, DistributedPubSub.Get(Context.System).Mediator, CurrentUtcTimestamper.Instance, new DistributedPubSubTradeEventSubscriptionManager(DistributedPubSub.Get(Context.System).Mediator))
        {
        }

        public MatchAggregator(string tickerSymbol, IActorRef mediator, ITimestamper timestamper, ITradeEventSubscriptionManager subscriptionManager)
        {
            TickerSymbol = tickerSymbol;
            _priceTopic = DistributedPubSubPriceTopicFormatter.PriceUpdateTopic(TickerSymbol);
            _volumeTopic = DistributedPubSubPriceTopicFormatter.VolumeUpdateTopic(TickerSymbol);
            _mediator = mediator;
            _timestamper = timestamper;
            _subscriptionManager = subscriptionManager;
            PersistenceId = EntityIdHelper.IdForPricing(tickerSymbol);
            
            Receives();
            Commands();
        }

        private void Receives()
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
            _publishPricesTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10), Self, PublishEvents.Instance, ActorRefs.NoSender);

            base.OnReplaySuccess();
        }

        private void RecoverAggregateData(MatchAggregatorSnapshot s)
        {
            _matchAggregate = new MatchAggregate(TickerSymbol, s.AvgPrice, s.AvgVolume);
            _priceUpdates.Enqueue(s.RecentPriceUpdates.ToArray());
            _volumeUpdates.Enqueue(s.RecentVolumeUpdates.ToArray());
        }

        private MatchAggregatorSnapshot SaveAggregateData()
        {
            return new MatchAggregatorSnapshot(_matchAggregate.AvgPrice.CurrentAvg, _matchAggregate.AvgVolume.CurrentAvg, _priceUpdates.ToList(), _volumeUpdates.ToList());
        }

        private void Commands()
        {
            Command<Match>(m => TickerSymbol.Equals(m.StockId), m =>
            {
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
                    _log.Info("Saved latest price {0} and volume {1}", snapshot.AvgPrice, snapshot.AvgVolume);
                    if (LastSequenceNr % SnapshotEveryN == 0)
                    {
                        SaveSnapshot(snapshot);
                    }
                });

                // publish updates to price and volume subscribers
                _mediator.Tell(new Publish(_priceTopic, latestPrice));
                _mediator.Tell(new Publish(_volumeTopic, latestVolume));
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
        }

        protected override void PostStop()
        {
            _publishPricesTask?.Cancel();
            base.PostStop();
        }
    }
}
