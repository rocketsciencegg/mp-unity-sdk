using System;

using UnityEngine.Scripting;

namespace RocketScience.Services.WireDirect.Tests.Stubs
{
    [Serializable]
    public class MessagePayload
    {
        public string message;

        [Preserve]
        public MessagePayload() {}
        public override string ToString()
        {
            return message;
        }
    }
}
