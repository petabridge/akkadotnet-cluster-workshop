using Xunit;

namespace Akka.CQRS.Tests.Serialization
{
    /// <summary>
    /// Helper class for writing <see cref="MemberDataAttribute"/> test cases.
    /// </summary>
    public static class XunitMemberDataHelper
    {
        public static object[] ToObjectArray(this object obj)
        {
            return new object[] { obj };
        }
    }
}