using UnityEngine;
using System.Threading.Tasks;

using RocketScience.Services.Multiplay.Apis.GameServer;

using RocketScience.Services.Multiplay.Http;
using Unity.Services.Core.Internal;
using Unity.Services.Authentication.Internal;
using RocketScience.Services.Wire.Internal;
using RocketScience.Services.Multiplay.Apis.Payload;

namespace RocketScience.Services.Multiplay
{
    internal class MultiplayServiceProvider
    {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            WireDirectServiceProvider.RegisterInitializeCallback(Initialize);
        }

        public static void Initialize(IWireDirect wds)
        {
            var httpClient = new HttpClient();

            MultiplayServiceSdk.Instance = new InternalMultiplayService(httpClient, wds, null);
            
        }
    }

    /// <summary>
    /// InternalMultiplayService
    /// </summary>
    internal class InternalMultiplayService : IMultiplayServiceSdk
    {
        /// <summary>
        /// Constructor for InternalMultiplayService
        /// </summary>
        /// <param name="httpClient">The HttpClient for InternalMultiplayService.</param>
        /// <param name="accessToken">The Authentication token for the service.</param>
        public InternalMultiplayService(HttpClient httpClient, IWireDirect wireDirect, IAccessToken accessToken = null)
        {
            GameServerApi = new GameServerApiClient(httpClient, accessToken);
            PayloadApi = new PayloadApiClient(httpClient, accessToken);
            WireDirect = wireDirect;
            ServerConfigReader = new ServerConfigReader();
            Configuration = new Configuration("http://127.0.0.1:8086", 10, 4, null);
        }

        /// <summary> Instance of IGameServerApiClient interface</summary>
        public IGameServerApiClient GameServerApi { get; set; }

        /// <summary> Instance of IPayloadApiclient interface</summary>
        public IPayloadApiClient PayloadApi { get; set; }

        public IWireDirect WireDirect { get; set; }

        public IServerConfigReader ServerConfigReader { get; set; }

        /// <summary> Configuration properties for the service.</summary>
        public Configuration Configuration { get; set; }
    }
}
