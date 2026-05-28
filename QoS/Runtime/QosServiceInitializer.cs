using Unity.Services.Authentication.Internal;
using RocketScience.Services.Qos.Apis.QosDiscovery;
using RocketScience.Services.Qos.Http;
using RocketScience.Services.Qos.Runner;

namespace RocketScience.Services.Qos
{
    class AccessTokenWrapper : IAccessToken
    {
        public string AccessToken { get; private set; }
        public AccessTokenWrapper(string token)
        {
            AccessToken = token;
        }
    }

    public class QosServiceInitializer
    {
        const bool k_UseStagingEnvironment = false;

        public static void Initialize(string authTokenStr)
        {
            QosService.SetInstance(new QosServiceInitializer().InitializeService(authTokenStr));
        }

        IQosService InitializeService(string authTokenStr)
        {
            // TODO(Jac Griffiths): Replace with our own auth (WorkOS)
            IAccessToken authToken = new AccessTokenWrapper(authTokenStr);

            var httpClient = new HttpClient();

            // Set up internal QoS Discovery API client & config
            var internalQosService = new InternalQosDiscoveryService(GetHost(), httpClient, authToken);

            var httpClientV2 = new Http.HttpClient();
            var v2Config = new Configuration(basePath: GetHost(), requestTimeout: 10, numRetries: 4, headers: null);

            // Set up public QoS interface
            var wrappedQosService = new WrappedQosService(internalQosService.QosDiscoveryApi, new BaselibQosRunner(), authToken);

            return wrappedQosService;
        }

        string GetHost()
        {
            // TODO(Josh HUghes): Replaced with new RS backend hostname, but may need further update for different environments

            return k_UseStagingEnvironment
                ? "https://qos.multiplay.dev"
                : "https://qos.multiplay.dev";
        }
    }

    /// <summary>
    /// InternalQosDiscoveryService
    /// </summary>
    class InternalQosDiscoveryService
    {
        const int RequestTimeout = 10;
        const int NumRetries = 4;

        /// <summary>
        /// Constructor for InternalQosDiscoveryService
        /// </summary>
        /// <param name="httpClient">The HttpClient for InternalQosDiscoveryService.</param>
        /// <param name="accessToken">The Authentication token for the service.</param>
        internal InternalQosDiscoveryService(string host, HttpClient httpClient, IAccessToken accessToken = null)
        {
            Configuration = new Configuration(host, RequestTimeout, NumRetries, null);

            QosDiscoveryApi = new QosDiscoveryApiClient(httpClient, accessToken, Configuration);
        }

        public IQosDiscoveryApiClient QosDiscoveryApi { get; set; }

        /// <summary> Configuration properties for the service.</summary>
        public Configuration Configuration { get; set; }
    }
}
