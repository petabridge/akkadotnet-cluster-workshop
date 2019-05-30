using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Petabridge.Cmd.Host;
using static Akka.CQRS.Pricing.Cli.PricingCmd;

namespace Akka.CQRS.Pricing.Cli
{
    /// <summary>
    /// The <see cref="PetabridgeCmd"/> command palette handelr for <see cref="PricingCmd.PricingCommandPalette"/>.
    /// </summary>
    public sealed class PriceCommands : CommandPaletteHandler
    {
        private IActorRef _matchAggregatorRouter;

        public PriceCommands(IActorRef matchAggregatorRouter) : base(PricingCommandPalette)
        {
            _matchAggregatorRouter = matchAggregatorRouter;
            HandlerProps = Props.Create(() => new PriceCmdRouter(_matchAggregatorRouter));
        }

        public override Props HandlerProps { get; }
    }
}
