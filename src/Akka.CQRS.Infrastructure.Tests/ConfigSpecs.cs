using System;
using Akka.CQRS.Infrastructure.Ops;
using FluentAssertions;
using Xunit;

namespace Akka.CQRS.Infrastructure.Tests
{
    public class ConfigSpecs
    {
        [Fact]
        public void ShouldLoadOpsConfig()
        {
            var config = OpsConfig.GetOpsConfig();
            if (config.HasPath("akka.cluster")) // so we don't barf on lesson1 (/start)
            {
                config.GetConfig("akka.cluster").HasPath("split-brain-resolver.active-strategy").Should().BeTrue();
            }
            
        }
    }
}
