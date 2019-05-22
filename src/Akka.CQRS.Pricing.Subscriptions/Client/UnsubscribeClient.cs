namespace Akka.CQRS.Pricing.Subscriptions.Client
{
    /// <summary>
    /// Sent via the <see cref="ClusterClient"/>, so it can't have any <see cref="IActorRef"/>s
    /// contained inside it. Otherwise that'll result in additional Akka.Remote connections to the
    /// client being opened by the other members of the cluster.
    /// </summary>
    public sealed class UnsubscribeClient : IWithStockId
    {
        public UnsubscribeClient(string stockId)
        {
            StockId = stockId;
        }

        public string StockId { get; }
    }
}