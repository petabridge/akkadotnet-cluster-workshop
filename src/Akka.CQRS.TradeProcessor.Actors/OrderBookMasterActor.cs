using Akka.Actor;
using Akka.CQRS.Subscriptions.InMem;
using Akka.Persistence.Extras;

namespace Akka.CQRS.TradeProcessor.Actors
{
    /// <summary>
    /// Child-per entity parent for order books.
    /// </summary>
    public sealed class OrderBookMasterActor : ReceiveActor
    {
        public OrderBookMasterActor()
        {
            Receive<IWithStockId>(s =>
            {
                var orderbookActor = Context.Child(s.StockId).GetOrElse(() => StartChild(s.StockId));
                orderbookActor.Forward(s);
            });

            Receive<IConfirmableMessageEnvelope<IWithStockId>>(s =>
            {
                var orderbookActor = Context.Child(s.Message.StockId).GetOrElse(() => StartChild(s.Message.StockId));
                orderbookActor.Forward(s);
            });
        }

        private IActorRef StartChild(string stockTickerSymbol)
        {
            var pub = new InMemoryTradeEventPublisher();
            return Context.ActorOf(Props.Create(() => new OrderBookActor(stockTickerSymbol, pub)), stockTickerSymbol);
        }
    }
}
