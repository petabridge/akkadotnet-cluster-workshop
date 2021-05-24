# Akka.CQRS
This code sample is part of Petabridge's [Akka.NET, Akka.Cluster, Kubernetes, and Docker Workshop](https://petabridge.com/cluster/) - if you'd like to follow along with the exercises, please go there!

## Building and Deploying
Please see our build instructions here: https://petabridge.com/cluster/building-docker-images.html

## Architecture Overview
Akka.CQRS is a reference architecture for [Akka.NET](https://getakka.net/), intended to illustrate the following Akka.NET techniques and principles:

1. [Command-Query Responsibility Segregation](https://docs.microsoft.com/en-us/azure/architecture/patterns/cqrs) - the Akka.NET actors who consume write events use distinctly different interfaces from those who consume read events
2. [Akka.Cluster](https://getakka.net/articles/clustering/cluster-overview.html) - a module that allows Akka.NET developers to create horizontally scalable, peer-to-peer, fault-tolerant, and elastic networks of Akka.NET actors.
3. [Akka.Cluster.Sharding](https://getakka.net/articles/clustering/cluster-sharding.html) - a fault-tolerant, distributed tool for maintaining a single source of truth for all domain entities. 
4. [Akka.Persistence](https://getakka.net/articles/persistence/event-sourcing.html) - a database-agnostic event-sourcing engine Akka.NET actors can use to persist and recover their data, thereby making it possible to move a persistent entity actor from one node in the cluster to another.
6. [Akka.Cluster.Tools](https://getakka.net/articles/clustering/distributed-publish-subscribe.html) - this sample makes use of `DistributedPubSub` for publishing events across the different nodes in the cluster and `ClusterSingleton`, to ensure that all read-side entities are up and running at all times.
7. [Petabridge.Cmd](https://cmd.petabridge.com/) - a command-line interface for Akka.NET that we use for watching multiple nodes in the cluster all maintain their own eventually consistent, but independent views of the read-side data produced by Akka.Persistence.Query.
8. [Akka.Bootstrap.Docker](https://github.com/petabridge/akkadotnet-bootstrap/tree/dev/src/Akka.Bootstrap.Docker) - this sample uses Docker and `docker-compose` to run the sample, and Akka.Bootstrap.Docker is used to inject runtime environment variables into the Akka.NET HOCON configuration at run-time.

#### Akka.Cluster.Sharding and Message Routing
In both the Trading and Pricing Services domains, we make heavy use of Akka.Cluster.Sharding in order to guarantee that there's a single instance of a particular domain entity present in the cluster at any given time.

A brief overview of Akka.Cluster.Sharding: 

1. Every entity type has its own `ShardRegion` - so in the case of the Trading Services, we have "orderBook" entities - each one representing the order book for a specific stock ticker symbol. In the Pricing Services we have "priceAggregator" entities.
2. A `ShardRegion` can host an arbitrary number of entity actors, defined using the `Props` passed into the `ClusterSharding.Start` method - each one of these entity actors represents a globally unique entity of their shardRegion type.
3. The `ShardRegion` aggregates these entity actors underneath parent root actors called "shards" - a shard is just an arbitrarily large number of entity actors grouped together for the purposes of ease-of-distribution across the Cluster. [The `ShardRegion` distributes these shards evenly across the cluster and will re-balance the distribution of shards in the event of a new node joining the cluster or an old node leaving](https://petabridge.com/blog/cluster-sharding-technical-overview-akkadotnet/).
4. In the event of a `ShardRegion` node becoming unreachable due to a network partition, node of the shards and entity actors on the unreachable node will be moved until that node (a) becomes reachable again or (b) [is marked as DOWN by another node in the cluster and kicked out](https://petabridge.com/blog/proper-care-of-akkadotnet-clusters/). This is done in order to guarantee that there's never more than 1 instance of a given entity actor at any time.
5. Any entity actors hosted by a `ShardRegion` can be accessed from other non-`ShardRegion` nodes through the use of a `ShardRegionProxy`, a router that uses the same message distribution mechanism as the `ShardRegion`. Therefore, sharded entity actors are always accessible to anyone in the cluster.
6. In the event that a shard and its entities are moved onto a new node, all of the messages intended for entity actors hosted on the affected shards are buffered by the `ShardRegion` and the `ShardRegionProxy` and released only once the shard actors have been successfully recreated on their new node.

All of these mechanisms are designed to provide a high degree of consistency, fault tolerance, and ease-of-use for Akka.NET users - hence why we make heavy use of Akka.Cluster.Sharding in the Akka.CQRS code sample.

The key to making sharding work smoothly across all of the nodes in the cluster, however, is ensuring that the same `IMessageExtractor` implementation is available - which is what we did with the [`StockShardMsgRouter` inside Akka.CQRS.Infrastructure](src/Akka.CQRS.Infrastructure/StockShardMsgRouter.cs):

```csharp
/// <summary>
/// Used to route sharding messages to order book actors hosted via Akka.Cluster.Sharding.
/// </summary>
public sealed class StockShardMsgRouter : HashCodeMessageExtractor
{
    /// <summary>
    /// 3 nodes hosting order books, 10 shards per node.
    /// </summary>
    public const int DefaultShardCount = 30;

    public StockShardMsgRouter() : this(DefaultShardCount)
    {
    }

    public StockShardMsgRouter(int maxNumberOfShards) : base(maxNumberOfShards)
    {
    }

    public override string EntityId(object message)
    {
        if (message is IWithStockId stockMsg)
        {
            return stockMsg.StockId;
        }

        switch (message)
        {
            case ConfirmableMessage<Ask> a:
                return a.Message.StockId;
            case ConfirmableMessage<Bid> b:
                return b.Message.StockId;
            case ConfirmableMessage<Fill> f:
                return f.Message.StockId;
            case ConfirmableMessage<Match> m:
                return m.Message.StockId;
        }

        return null;
    }
}
```

This message extractor works by extracting the `StockId` property from messages with `IWithStockId` defined, since those are the events and commands we're sending to our sharded entity actors in both the Trading and Pricing services. It's worth noting, however, that we're also making use of the [`IComfirmableMessage`](https://devops.petabridge.com/api/Akka.Persistence.Extras.IConfirmableMessage.html) type from Akka.Persistence.Extras along with the `PersistenceSuperivsor` from that same package, hence why we've added handling for the `ConfirmableMessage<T>` types inside the `StockShardMsgRouter`.

> Rule of thumb: when trying to choose the number of shards you want to have in Akka.Cluster.Sharding, use the following formula: `max # of nodes who can host enities of this type * 10 = shard count`. This will give you a moderate amount of shards and ensure that re-balancing of shards doesn't happen too often and when it does happen it doesn't impact an unacceptably large number of entities all at once.

With this `StockShardMsgRouter` in-hand, we can [start our `ShardRegion` inside the Trading Services' "OrderBook" nodes](src/Akka.CQRS.TradeProcessor.Service/Program.cs#L42-L48):

```csharp
Cluster.Cluster.Get(actorSystem).RegisterOnMemberUp(() =>
{
    var sharding = ClusterSharding.Get(actorSystem);


    var shardRegion = sharding.Start("orderBook", s => OrderBookActor.PropsFor(s), ClusterShardingSettings.Create(actorSystem),
        new StockShardMsgRouter());
});
```

Or a [`ShardProxy` inside the Trading Service's "Trade Placer" nodes](src/Akka.CQRS.TradePlacers.Service/Program.cs#L32-L55):

```csharp
Cluster.Cluster.Get(actorSystem).RegisterOnMemberUp(() =>
{
    var sharding = ClusterSharding.Get(actorSystem);


    var shardRegionProxy = sharding.StartProxy("orderBook", "trade-processor", new StockShardMsgRouter());
    foreach (var stock in AvailableTickerSymbols.Symbols)
    {
        var max = (decimal)ThreadLocalRandom.Current.Next(20, 45);
        var min = (decimal) ThreadLocalRandom.Current.Next(10, 15);
        var range = new PriceRange(min, 0.0m, max);


        // start bidders
        foreach (var i in Enumerable.Repeat(1, ThreadLocalRandom.Current.Next(1, 6)))
        {
            actorSystem.ActorOf(Props.Create(() => new BidderActor(stock, range, shardRegionProxy)));
        }


        // start askers
        foreach (var i in Enumerable.Repeat(1, ThreadLocalRandom.Current.Next(1, 6)))
        {
            actorSystem.ActorOf(Props.Create(() => new AskerActor(stock, range, shardRegionProxy)));
        }
    }
});
```

One final, important thing to note about working with Akka.Cluster.Sharding - _we need to specify the roles that shards are hosted on_. Otherwise Akka.Cluster.Sharding will try to communicate with `ShardRegion` hosts on all nodes in the cluster. You can do this via the `ClusterShardingSettings` class or via the `akka.cluster.sharding` HOCON, which we did in this sample:

```
akka{
    # rest of HOCON configuration
    cluster {
        #will inject this node as a self-seed node at run-time
        seed-nodes = ["akka.tcp://AkkaTrader@127.0.0.1:5055"] 
        roles = ["trade-processor" , "trade-events"]

        pub-sub{
            role = "trade-events"
        }

        sharding{
            role = "trade-processor"
        }
    }
}
```

#### Dealing with Unreachable Nodes and Failover in Akka.Cluster.Sharding

One other major concern we need to address when working with Akka.Cluster.Sharding is ensuring that unreachable nodes in the cluster are downed quickly in the event that they don't recover. In most production scenarios, a node is only unreachable for a brief period of time - typically the result of a temporary network partition, therefore nodes usually recover back to a reachable state rather quickly. 

However, in the event of an issue like an outright hardware failure, a process crash, or an unclean shutdown (not letting the node leave the cluster gracefully prior to termination) then these "unreachable" nodes are truly permanently unavailable. Therefore, we should remove those nodes from the cluster's membership. Today you can do this manually with a tool like [Petabridge.Cmd.Cluster](https://cmd.petabridge.com/articles/commands/cluster-commands.html) via the `cluster down-unreachable` command, but a better method for accomplishing this is to [use the built-in Split Brain Resolvers inside Akka.Cluster](https://getakka.net/articles/clustering/split-brain-resolver.html).

Inside the [Akka.CQRS.Infrastructure/Ops folder we have an embedded HOCON file and a utility class for parsing it](src/Akka.CQRS.Infrastructure/Ops) - the HOCON file contains a straight-forward split brain resolver configuration that we standardize across all nodes in both the Trading Services and Pricing Services clusters.

```
# Akka.Cluster split-brain resolver configurations
akka.cluster{
    downing-provider-class = "Akka.Cluster.SplitBrainResolver, Akka.Cluster"
    split-brain-resolver {
        active-strategy = keep-majority
    }
}
```

The `keep-majority` strategy works as follows: if there's 10 nodes and 4 suddenly become unreachable, the remaining part of the cluster with 6 nodes still standing kicks the remaining 4 nodes out of the cluster via a DOWN command if those nodes have been unreachable for longer than 45 seconds ([read the documentation for a full explanation of the algorithm, including tie-breakers.](https://getakka.net/articles/clustering/split-brain-resolver.html))

We then ensure via the `OpsConfig` class that this configuration is used _uniformly throughout all of our cluster nodes_, since any of those nodes can be the leader of the cluster and it's the leader who has to execute the split brain resolver strategy code. You can see an example here where we [start up the Pricing Service in its `Program.cs`](https://github.com/Aaronontheweb/InMemoryCQRSReplication/blob/3cbf46da0cf4e9735204c2750e2df9e3bead3eca/src/Akka.CQRS.Pricing.Service/Program.cs#L41-L44):

```csharp
// get HOCON configuration
var conf = ConfigurationFactory.ParseString(config).WithFallback(GetMongoHocon(mongoConnectionString))
    .WithFallback(OpsConfig.GetOpsConfig())
    .WithFallback(ClusterSharding.DefaultConfig())
    .WithFallback(DistributedPubSub.DefaultConfig());
```

When this is used in combination with Akka.Cluster.Sharding the split brain resolver guarantees that no entity in a `ShardRegion` will be unavailable for longer than the split brain resolver's downing duration. This works because whenever an unreachable `ShardRegion` node is DOWNed, all of its shards will be automatically re-allocated onto one or more of the other available `ShardRegion` host nodes remaining in the cluster. It provides automatic fault-tolerance even in the case of total loss of availability for one or more affected nodes.

### Trading Services Domain
The write-side cluster, the Trading Services are primarily interested in the placement and matching of new trade orders for buying and selling of specific stocks.

The Trading Services are driven primarily through the use of three actor types:

1. [`BidderActor`](src/Akka.CQRS.TradeProcessor.Actors/BidderActor.cs) - runs inside the "Trade Placement" services and randomly bids on a specific stock;
2. [`AskerActor`](rc/Akka.CQRS.TradeProcessor.Actors/AskerActor.cs) - runs inside the "Trade Placement" services and randomly asks (sells) a specific stock; and
3. [`OrderBookActor`](src/Akka.CQRS.TradeProcessor.Actors/OrderBookActor.cs) - the most important actor in this scenario, it is hosted on the "Trace Processor" service and it's responsible for matching bids with asks, and when it does it publishes `Match` and `Fill` events across the cluster using `DistributedPubSub`. This is how the `AskerActor` and the `BidderActor` involved in making the trade are notified that their trades have been settled. All events received and produced by the `OrderBookActor` are persisted using Akka.Persistence.MongoDb.

The domain design is relatively simple otherwise and we'd encourage you to look at the code directly for more details about how it all works. 

### Pricing Services Domain
The read-side cluster, the Pricing Services consume the `Match` events for specific ticker symbols produced by the `OrderBookActor`s inside the Trading Services domain by receiving the events when they're published via `DistributedPubSub`.

The [`MatchAggregator` actors hosted inside Akka.Cluster.Sharding on the Pricing Services nodes](src/Akka.CQRS.Pricing.Actors/MatchAggregator.cs) are the ones who actually consume `Match` events and aggregate the `Match.SettlementPrice` and `Match.Quantity` to produce an estimated, weighted moving average of both volume and price. 

These actors also use `DistributedPubSub` to periodically publish `IPriceUpdate` events out to the rest of the Pricing Services cluster:

```csharp
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

    // publish updates to in-memory replicas
    _mediator.Tell(new Publish(_priceTopic, latestPrice));
    _mediator.Tell(new Publish(_volumeTopic, latestVolume));
});
```

## Running Akka.CQRS
Akka.CQRS can be run easily using Docker and `docker-compose`. You'll want to make sure you have [Docker for Windows](https://docs.docker.com/docker-for-windows/) installed with Linux containers enabled if you're running on a Windows PC.

First, clone this repository and run the [`build.cmd` script](build.cmd) in the root of this repository:

```
PS> ./build.cmd docker
```

The `docker` build stage will create three Docker images locally:

* `akka.cqrs.tradeprocessor`
* `akka.cqrs.traders`
* `akka.cqrs.pricing`

All of these Docker images will be tagged with the `latest` tag and the version number found at the topmost entry of [`RELEASE_NOTES.md`](RELEASE_NOTES.md).

From there, we can start up both cluster via `docker-compose`:

```
PS> docker-compose up
```

This will create both clusters, the MongoDb database they depend on, and will also expose the Pricing node's `Petabridge.Cmd.Host` port on a randomly available port. [See the `docker-compose.yaml` file for details](docker-compose.yaml).

### Testing the Consistency and Failover Capabilities of Akka.CQRS
If you want to test the consistency and fail-over capabilities of this sample, then start by [installing the `pbm` commandline tool](https://cmd.petabridge.com/articles/install/index.html):

```
PS> dotnet tool install --global pbm 
```

Next, connect to the first one of the pricing nodes we have running inside of Docker. If you're using [Kitematic, part of Docker for Windows](https://kitematic.com/), you can see the random port that the `akka.cqrs.pricing` containers expose their port on by clicking on the running container instance and going to **Networking**:

![Docker for Windows host container networking](/docs/images/docker-for-windows-networking.png)

Connect to `Petabridge.Cmd` on this node via the following command:

```
PS> pbm 127.0.0.1:32773 (in this instance - your port number will be chosen at random by docker)
```

Once you're connected, you'll be able to access all sorts of information about this Pricing node, including [accessing some customer Petabridge.Cmd palettes designed specifically for accessing stock price information inside this sample](rc/Akka.CQRS.Pricing.Cli).

Go ahead and start a price-tracking command and see how it goes:

```
pbm> price track -s MSFT
```

Next, use `docker-compose` to bring up a few more Pricing containers in another terminal window:

```
PS> docker-compose up scale pricing-engine=4
```

This will create another 3 containers running the `akka.cqrs.pricing` image - all of them will join the cluster automatically, which you can verify using a `cluster show` command in Petabridge.Cmd.

Once this is done, start two more terminals and _connect to two of the new `akka.cqrs.pricing` nodes you just started_ and execute the same `price track -s MSFT` command. You should see that the stream of updated prices is uniform across all three nodes.

Now go and kill the original node you were connected to - the first one of the `akka.cqrs.pricing` nodes you were connected to. This node definitely has some shards hosted on it, if not all of the shards given how few entities there are, so this will prompt a fail-over to happen and for the sharded entity to move across the cluster. What you should see is that the pricing data for the other two nodes you're connected to remains consistent both before, during, and after the fail-over. It may have taken some time for the new `MatchAggregator` to come online and begin updating prices again, but once it came back online the `PriceVolumeViewActor`s that the Akka.CQRS.Pricing.Cli commands uses were able to re-acquire their pricing information from Akka.Cluster.Sharding and continue running normally.

##### Known Issues
This sample is currently affected by https://github.com/akkadotnet/akka.net/issues/3414, so you will need to explicitly delete the MongoDb container (not just stop it - DELETE it) every time you restart this Docker cluster from scratch. We're working on fixing that issue and should have a patch for it shortly.
