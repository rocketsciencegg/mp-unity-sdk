using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RocketScience.Services.Wire.Internal
{
    internal class SubscriptionHandle
    {
        public string ChannelName { get; }

        public SubscriptionHandle(string channelName)
        {
            ChannelName = channelName;
        }
    }
}
