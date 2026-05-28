using System;
using System.Collections;
using RocketScience.Networking.Qos;

namespace RocketScience.Services.Qos.Tests
{
    // Since we only need these Equals methods in tests, creating them here as extension methods
    // instead of on the object directly
    static class TestExtensions
    {
        internal static bool Equals(this QosResult thisResult, object obj)
        {
            if (obj.GetType() != typeof(QosResult))
            {
                return false;
            }

            var other = (QosResult)obj;

            return thisResult.Region == other.Region && thisResult.AverageLatencyMs == other.AverageLatencyMs &&
                Math.Abs(thisResult.PacketLossPercent - other.PacketLossPercent) < 0.01;
        }

        internal static bool Equals(this UcgQosServer thisServer, object obj)
        {
            if (obj.GetType() != typeof(UcgQosServer))
            {
                return false;
            }

            var other = (UcgQosServer)obj;

            return thisServer.regionid == other.regionid && thisServer.ipv4 == other.ipv4 && thisServer.ipv6 == other.ipv6 && thisServer.port == other.port && thisServer.BackoffUntilUtc.Equals(other.BackoffUntilUtc);
        }
    }

    // Custom comparer since extension methods don't seem to easily work on interfaces
    // or in cases where the type is not always obvious.
    class IQosResultComparer : IComparer
    {
        public int Compare(object x, object y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (ReferenceEquals(null, y))
            {
                return 1;
            }

            if (ReferenceEquals(null, x))
            {
                return -1;
            }

            var typedX = (IQosResult)x;
            var typedY = (IQosResult)y;

            var regionComparison = string.Compare(typedX.Region, typedY.Region, StringComparison.Ordinal);
            if (regionComparison != 0)
            {
                return regionComparison;
            }

            var averageLatencyMsComparison = typedX.AverageLatencyMs.CompareTo(typedY.AverageLatencyMs);
            if (averageLatencyMsComparison != 0)
            {
                return averageLatencyMsComparison;
            }

            return Math.Abs(typedX.PacketLossPercent - typedY.PacketLossPercent) < 0.01 ? 0 : 1;
        }
    }
}
