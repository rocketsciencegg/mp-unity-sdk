using RocketScience.Services.Wire.Internal;

namespace RocketScience.Services.WireDirect.Tests
{
    class TestBackoffStrategy : IBackoffStrategy
    {
        public float GetNext()
        {
            return 0;
        }

        public void Reset()
        {
            // no need to do anything
        }
    }
}
