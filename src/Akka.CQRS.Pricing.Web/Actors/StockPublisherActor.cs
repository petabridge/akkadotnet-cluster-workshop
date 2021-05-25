using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.CQRS.Pricing.Events;
using Akka.CQRS.Pricing.Web.Hubs;
using Akka.Event;

namespace Akka.CQRS.Pricing.Web.Actors
{
    /// <summary>
    /// Publishes events directly to the <see cref="StockHubHelper"/>
    /// </summary>
    public class StockPublisherActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly StockHubHelper _hub;

        public StockPublisherActor(StockHubHelper hub)
        {
            _hub = hub;

            ReceiveAsync<IPriceUpdate>(async p =>
            {
                try
                {
                    _log.Info("Received event {0}", p);
                    await hub.WritePriceChanged(p);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error while writing price update [{0}] to StockHub", p);
                }
            });

            ReceiveAsync<IVolumeUpdate>(async p =>
            {
                try
                {
                    _log.Info("Received event {0}", p);
                    await hub.WriteVolumeChanged(p);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error while writing volume update [{0}] to StockHub", p);
                }
            });
        }
    }
}
