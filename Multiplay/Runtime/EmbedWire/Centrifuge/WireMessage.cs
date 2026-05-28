using UnityEngine.Scripting;

namespace RocketScience.Services.Wire.Internal
{
    class WireMessage
    {
        [Preserve]
        public WireMessage()
        {
        }

        public string payload;
        public string version;
    }
}
