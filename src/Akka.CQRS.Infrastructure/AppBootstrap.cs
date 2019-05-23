using System;
using System.Collections.Generic;
using System.Text;
using Akka.Configuration;

namespace Akka.CQRS.Infrastructure
{
    /// <summary>
    /// Used to boostrap the start of <see cref="ActorSystem"/>s by injecting their configs
    /// with the relevant bits and pieces.
    /// </summary>
    public static class AppBootstrap
    {
        public static Config BoostrapApplication(this Config c, AppBootstrapConfig appConfig)
        {
            return c;
        }
    }

    public sealed class AppBootstrapConfig
    {
        public AppBootstrapConfig(bool needPersistence = true, bool needClustering = true)
        {
            NeedPersistence = needPersistence;
            NeedClustering = needClustering;
        }

        public bool NeedPersistence { get; }

        public bool NeedClustering { get; }
    }
}
