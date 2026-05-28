using System.Threading.Tasks;

using Unity.Services.Wire.Internal;

namespace RocketScience.Services.WireDirect.Tests
{
    class TokenProvider : IChannelTokenProvider
    {
        public ChannelToken data;
        public TokenProvider(string channel, string token)
        {
            data = new ChannelToken() { ChannelName = channel, Token = token };
        }

        public TokenProvider()
        {
            data = new ChannelToken() { ChannelName = "test", Token = "abc" };
        }

        public Task<ChannelToken> GetTokenAsync()
        {
            return Task.FromResult(data);
        }
    }
}
