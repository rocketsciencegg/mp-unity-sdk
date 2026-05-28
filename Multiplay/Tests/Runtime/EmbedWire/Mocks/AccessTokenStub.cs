using Unity.Services.Authentication.Internal;

namespace RocketScience.Services.WireDirect.Tests
{
    class AccessTokenStub : IAccessToken
    {
        public string AccessToken { get => "my-access-token"; }
    }
}
