using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.CQRS.Pricing.Web.Hubs;

namespace Akka.CQRS.Pricing.Web.Services
{
    /// <summary>
    /// Used to launch the <see cref="ActorSystem"/> and actors needed
    /// to communicate with the rest of the cluster.
    /// </summary>
    public sealed class AkkaService
    {
        public void StartActorSystem(StockHubHelper helper)
        {

        }
    }
}
