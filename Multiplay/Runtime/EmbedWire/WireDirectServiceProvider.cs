using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Internal;
using Unity.Services.Core.Scheduler.Internal;
using Unity.Services.Core.Threading.Internal;
using Unity.Services.Core.Telemetry.Internal;
using UnityEngine;
using System;

namespace RocketScience.Services.Wire.Internal
{
    public class WireDirectServiceProvider : IInitializablePackage
    {
        private static Action<IWireDirect> _onWireInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            // Pass an instance of this class to Core
            var generatedPackageRegistry =
                CoreRegistry.Instance.RegisterPackage(new WireDirectServiceProvider());
            // And specify what components it requires, or provides.
            generatedPackageRegistry
                .DependsOn<IActionScheduler>()
                .DependsOn<IUnityThreadUtils>()
                .DependsOn<IMetricsFactory>()
                .OptionallyDependsOn<IAccessToken>()
                .ProvidesComponent<IWireDirect>();
        }

        public Task Initialize(CoreRegistry registry)
        {
            var actionScheduler = registry.GetServiceComponent<IActionScheduler>();
            var threadUtils = registry.GetServiceComponent<IUnityThreadUtils>();
            WireDirect wds = new WireDirect(actionScheduler, null, threadUtils, null);
            registry.RegisterServiceComponent<IWireDirect>(wds);
            _onWireInitialized?.Invoke(wds);
            return Task.CompletedTask;
        }

        public static void RegisterInitializeCallback(Action<IWireDirect> callback)
        {
            _onWireInitialized += callback;
        }

        public static void UnregisterInitializeCallback(Action<IWireDirect> callback)
        {
            _onWireInitialized -= callback;
        }
    }
}
