using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.CQRS.Pricing.Events;
using Microsoft.AspNetCore.SignalR;

namespace Akka.CQRS.Pricing.Web.Hubs
{
    /// <summary>
    /// Used by actors to publish data directly to SignalR.
    /// </summary>
    public class StockHubHelper
    {
        private readonly IHubContext<StockHub> _hub;

        public StockHubHelper(IHubContext<StockHub> hub)
        {
            _hub = hub;
        }

        public async Task WriteVolumeChanged(IVolumeUpdate e)
        {
            await WriteMessage(e.ToString());
        }

        public async Task WritePriceChanged(IPriceUpdate e)
        {
            await WriteMessage(e.ToString());
        }

        internal async Task WriteMessage(string message)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await _hub.Clients.All.SendAsync("writeEvent", message, cts.Token);
        }
    }
}
