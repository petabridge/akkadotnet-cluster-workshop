using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using static Akka.CQRS.Pricing.Subscriptions.DistributedPubSub.DistributedPubSubPriceTopicFormatter;

namespace Akka.CQRS.Pricing.Subscriptions.DistributedPubSub
{
    /// <summary>
    /// <see cref="IMarketEventPublisher"/> used for distributing events over the <see cref="DistributedPubSub.Mediator"/>.
    /// </summary>
    public sealed class DistributedPubSubMarketEventPublisher : IMarketEventPublisher
    {
        private readonly IActorRef _mediator;

        public DistributedPubSubMarketEventPublisher(IActorRef mediator)
        {
            _mediator = mediator;
        }

        public void Publish(string tickerSymbol, IMarketEvent @event)
        {
            var eventType = @event.ToMarketEventType();
            var topic = ToTopic(tickerSymbol, eventType);
            _mediator.Tell(new Publish(topic, @event));
        }

        public static DistributedPubSubMarketEventPublisher For(ActorSystem sys)
        {
            var mediator = Cluster.Tools.PublishSubscribe.DistributedPubSub.Get(sys).Mediator;
            return new DistributedPubSubMarketEventPublisher(mediator);
        }
    }
}