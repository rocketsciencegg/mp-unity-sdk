#if NUGET_MOQ_AVAILABLE && UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Core.Device.Internal;
using Unity.Services.Core.Environments.Internal;
using Unity.Services.Core.Telemetry.Internal;
using UnityEngine.TestTools;
using Unity.Services.Core.TestUtils;

namespace RocketScience.Services.Qos
{
    [SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod")]
    class InitializationTests
    {
        /// <summary>
        /// The package we will run tests on.
        /// </summary>
        QosPackageInitializer m_Package;

        /// <summary>
        /// A method called before every test to make sure everything's in order to run your test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            // Create the package we will run tests on.
            m_Package = new QosPackageInitializer();

            // Make sure your context is ready for your test.
            QosService.Instance = null;
        }

        /// <summary>
        /// A method called after every test to cleanup any unwanted remaining changes.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            // Cleanup your static state when your test is done.
            QosService.Instance = null;
        }

        /// <summary>
        /// A test to validate that your package initializer behaves as expected when ran in optimal conditions.
        /// </summary>
        [UnityTest]
        public IEnumerator InitializeInOptimistPathSucceeds()
        {
            // ServicesCoreScope is an utility to simulate a Core context (UnityServices, CoreRegistry, ...)
            using (var testCore = new ServicesCoreScope())
            {
                // Be sure to register providers for your package's dependencies if you want to test the happy path.
                RegisterFakeProviders(testCore);

                // Register your package to your test registry.
                // Required only if your package has optional dependencies (declared by using OptionallyDependsOn<>()).
                m_Package.Register(testCore.Registry);

                // Initialize your package using the simulated Core.
                var initialization = testCore.InitializePackageAsync(m_Package);

                // Since task tests are not supported yet, we have to manually yield until the task completes.
                while (!initialization.IsCompleted)
                {
                    yield return null;
                }

                // Assert your initializer behaved as expected.
                Assert.AreEqual(TaskStatus.RanToCompletion, initialization.Status);
                Assert.IsNotNull(QosService.Instance);
            }
        }

        /// <summary>
        /// A test to validate that your package initializer behaves as expected when initialized multiple times.
        /// </summary>
        [UnityTest]
        public IEnumerator InitializeTwicePutsServiceInExpectedState()
        {
            // ServicesCoreScope is an utility to simulate a Core context (UnityServices, CoreRegistry, ...)
            using (var testCore = new ServicesCoreScope())
            {
                // Be sure to register providers for your package's dependencies if you want to test the happy path.
                RegisterFakeProviders(testCore);

                // Register your package to your test registry.
                // Required only if your package has optional dependencies (declared by using OptionallyDependsOn<>()).
                m_Package.Register(testCore.Registry);

                // Initialize your package using the simulated Core.
                var initialization = testCore.InitializePackageAsync(m_Package);

                // Since task tests are not supported yet, we have to manually yield until the task completes.
                while (!initialization.IsCompleted)
                {
                    yield return null;
                }

                // Assert your initializer behaved as expected.
                Assert.AreEqual(TaskStatus.RanToCompletion, initialization.Status);
                Assert.IsNotNull(QosService.Instance);

                // Setup before 2nd initialization: This is specific for each service. In this example we make
                // sure each initialization sets a different instance to OperateTemplateService.Instance.
                var firstInitializationInstance = QosService.Instance;

                // Re-initialize your package using the simulated Core.
                initialization = testCore.InitializePackageAsync(m_Package);
                while (!initialization.IsCompleted)
                {
                    yield return null;
                }

                // Assert your initializer behaved as expected on a second call.
                Assert.AreEqual(TaskStatus.RanToCompletion, initialization.Status);
                Assert.IsNotNull(QosService.Instance);
                Assert.AreNotSame(firstInitializationInstance, QosService.Instance);
            }
        }

        /// <summary>
        /// Register fake providers for all dependencies of <see cref="TemplatePackageInitializer"/>.
        /// </summary>
        static void RegisterFakeProviders(ServicesCoreScope testCore)
        {
            // Be sure to explicitly use the component's interface to RegisterProviderFor<IComponentInterface>().
            testCore.RegisterProviderFor<IMetricsFactory>(CreateFakeMetricsFactory());
            testCore.RegisterProviderFor<IDiagnosticsFactory>(CreateFakeDiagnosticsFactory());
            testCore.RegisterProviderFor<IProjectConfiguration>(CreateFakeProjectConfiguration());
            testCore.RegisterProviderFor<IInstallationId>(CreateFakeInstallationId());
            testCore.RegisterProviderFor<IEnvironments>(CreateFakeEnvironments());
            testCore.RegisterProviderFor<IAccessToken>(CreateFakeAccessToken());
            testCore.RegisterProviderFor<IPlayerId>(CreateFakePlayerId());

            // All following functions are mocking details specific to this template.
            IMetricsFactory CreateFakeMetricsFactory()
            {
                var mock = new Mock<IMetricsFactory>();
                mock.Setup(x => x.Create(It.IsAny<string>()))
                    .Returns(CreateFakeMetrics());
                return mock.Object;
            }

            IMetrics CreateFakeMetrics()
            {
                var mock = new Mock<IMetrics>();
                mock.Setup(x => x.SendGaugeMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()));
                return mock.Object;
            }

            IDiagnosticsFactory CreateFakeDiagnosticsFactory()
            {
                var mock = new Mock<IDiagnosticsFactory>();
                mock.Setup(x => x.Create(It.IsAny<string>()))
                    .Returns(CreateFakeDiagnostics());
                return mock.Object;
            }

            IDiagnostics CreateFakeDiagnostics()
            {
                var mock = new Mock<IDiagnostics>();
                mock.Setup(x => x.SendDiagnostic(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()));
                return mock.Object;
            }

            IProjectConfiguration CreateFakeProjectConfiguration()
            {
                var mock = new Mock<IProjectConfiguration>();
                mock.Setup(x => x.GetBool(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns<string, bool>((key, defaultValue) => defaultValue);
                return mock.Object;
            }

            IInstallationId CreateFakeInstallationId()
            {
                var mock = new Mock<IInstallationId>();
                mock.Setup(x => x.GetOrCreateIdentifier())
                    .Returns("");
                return mock.Object;
            }

            IEnvironments CreateFakeEnvironments()
            {
                var mock = new Mock<IEnvironments>();
                mock.Setup(x => x.Current)
                    .Returns("");
                return mock.Object;
            }

            IAccessToken CreateFakeAccessToken()
            {
                var mock = new Mock<IAccessToken>();
                mock.Setup(x => x.AccessToken)
                    .Returns("");
                return mock.Object;
            }

            IPlayerId CreateFakePlayerId()
            {
                var mock = new Mock<IPlayerId>();
                mock.Setup(x => x.PlayerId)
                    .Returns("");
                return mock.Object;
            }
        }
    }
}
#endif
