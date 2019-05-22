// -----------------------------------------------------------------------
// <copyright file="PriceCmdRouter.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.CQRS.Pricing.Commands;
using Akka.CQRS.Pricing.Views;
using Akka.Event;
using Petabridge.Cmd;
using Petabridge.Cmd.Host;

namespace Akka.CQRS.Pricing.Cli
{
    /// <summary>
    /// Actor responsible for carrying out <see cref="PricingCmd.PricingCommandPalette"/> commands.
    /// </summary>
    public sealed class PriceCmdRouter : CommandHandlerActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private IActorRef _priceViewMaster;

        public PriceCmdRouter(IActorRef priceViewMaster) : base(PricingCmd.PricingCommandPalette)
        {
            _priceViewMaster = priceViewMaster;

            Process(PricingCmd.TrackPrice.Name, (command, arguments) =>
            {
                var tickerSymbol = arguments.ArgumentValues("symbol").Single();

                // the tracker actor will start automatically recording price information on its own. No further action needed.
                var trackerActor =
                    Context.ActorOf(Props.Create(() => new PriceTrackingActor(tickerSymbol, _priceViewMaster, Sender)));
            });

            Process(PricingCmd.PriceHistory.Name, (command, arguments) =>
            {
                var tickerSymbol = arguments.ArgumentValues("symbol").Single();
                var getPriceTask = _priceViewMaster.Ask<PriceAndVolumeSnapshot>(new FetchPriceAndVolume(tickerSymbol), TimeSpan.FromSeconds(5));
                var sender = Sender;

                // pipe happy results back to the sender only on successful Ask
                getPriceTask.ContinueWith(tr =>
                {
                    _log.Info("Got price history back for {0}", tr.Result.StockId);
                    try
                    {
                        if (tr.Result.PriceUpdates.Length == 0)
                            return new[]
                                {new CommandResponse($"No historical price data available for [{tr.Result.StockId}]")};
                        return Enumerable.Select(tr.Result.PriceUpdates, x => new CommandResponse(x.ToString(), false))
                            .Concat(new[] { CommandResponse.Empty });
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Exception while returning price history for {0}", tickerSymbol);
                        return new[] {CommandResponse.Empty};
                    }
                    
                }, TaskContinuationOptions.OnlyOnRanToCompletion).PipeTo(sender);

                // pipe unhappy results back to sender on failure
                getPriceTask.ContinueWith(tr => 
                        new ErroredCommandResponse($"Error while fetching price history for {tickerSymbol} - " +
                                                   $"timed out after 5s"), TaskContinuationOptions.NotOnRanToCompletion)
                    .PipeTo(sender); ;
            });
        }
    }
}