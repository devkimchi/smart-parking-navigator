#pragma warning disable ASPIREPROBES001

using Aspire.Hosting;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Azure.AppContainers;

namespace CarparkAvailability.AppHost.Tests;

public class AppHostCompositionTests
{
    private const string TestGoogleMapsApiKey = "test-google-maps-key";
    private const string TestDataGovSgApiKey = "test-data-gov-sg-key";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    private static readonly string[] s_testArguments =
    [
        $"--GoogleMaps:ApiKey={TestGoogleMapsApiKey}",
        $"--DataGovSg:ApiKey={TestDataGovSgApiKey}"
    ];
    private static readonly string[] s_publishArguments = [.. s_testArguments, "--publisher=manifest"];

    [Fact]
    public async Task AppHostDefinesExpectedTopologyAndSecretFlow()
    {
        using CancellationTokenSource cancellationTokenSource = new(DefaultTimeout);
        IDistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.CarparkAvailability_AppHost>(
                s_testArguments,
                cancellationTokenSource.Token);

        ProjectResource api = Assert.IsType<ProjectResource>(
            Assert.Single(appHost.Resources, static resource => resource.Name == "apiapp"));
        ProjectResource web = Assert.IsType<ProjectResource>(
            Assert.Single(appHost.Resources, static resource => resource.Name == "webapp"));

        ParameterResource dataGovSgApiKey = Assert.Single(
            appHost.Resources.OfType<ParameterResource>(),
            static parameter => parameter.Name == "data-gov-sg-api-key");
        ParameterResource googleMapsApiKey = Assert.Single(
            appHost.Resources.OfType<ParameterResource>(),
            static parameter => parameter.Name == "google-maps-api-key");
        Assert.True(dataGovSgApiKey.Secret);
        Assert.True(googleMapsApiKey.Secret);
        Assert.Equal(
            TestDataGovSgApiKey,
            await dataGovSgApiKey.GetValueAsync(cancellationTokenSource.Token));
        Assert.Equal(
            TestGoogleMapsApiKey,
            await googleMapsApiKey.GetValueAsync(cancellationTokenSource.Token));

        Assert.All(
            api.Annotations.OfType<EndpointAnnotation>()
                .Where(static endpoint => endpoint.UriScheme is "http" or "https"),
            static endpoint => Assert.False(endpoint.IsExternal));
        Assert.All(
            web.Annotations.OfType<EndpointAnnotation>()
                .Where(static endpoint => endpoint.UriScheme is "http" or "https"),
            static endpoint => Assert.True(endpoint.IsExternal));

        AssertProbes(api);
        AssertProbes(web);

        WaitAnnotation wait = Assert.Single(web.Annotations.OfType<WaitAnnotation>());
        Assert.Same(api, wait.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, wait.WaitType);

        Dictionary<string, object> apiEnvironment = await GatherEnvironmentAsync(
            api,
            appHost.ExecutionContext,
            cancellationTokenSource.Token);
        Dictionary<string, object> webEnvironment = await GatherEnvironmentAsync(
            web,
            appHost.ExecutionContext,
            cancellationTokenSource.Token);

        Assert.Same(dataGovSgApiKey, apiEnvironment["Availability__ApiKey"]);
        Assert.DoesNotContain("GoogleMaps__ApiKey", apiEnvironment);
        Assert.Same(googleMapsApiKey, webEnvironment["GoogleMaps__ApiKey"]);
        Assert.DoesNotContain("Availability__ApiKey", webEnvironment);
        Assert.Contains(
            webEnvironment,
            static variable => variable.Key.StartsWith("services__apiapp__", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishTopologyTargetsSingleReplicaContainerApps()
    {
        using CancellationTokenSource cancellationTokenSource = new(DefaultTimeout);
        IDistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.CarparkAvailability_AppHost>(
                s_publishArguments,
                cancellationTokenSource.Token);

        Assert.True(appHost.ExecutionContext.IsPublishMode);
        Assert.Single(appHost.Resources.OfType<AzureContainerAppEnvironmentResource>());
        Assert.DoesNotContain(
            appHost.Resources,
            static resource => resource.GetType().Name.Contains("KeyVault", StringComparison.Ordinal));

        ProjectResource[] projects = [.. appHost.Resources.OfType<ProjectResource>()];
        Assert.Equal(2, projects.Length);
        Assert.All(
            projects,
            static project => Assert.Single(
                project.Annotations.OfType<AzureContainerAppCustomizationAnnotation>()));

        ProjectResource api = Assert.Single(projects, static project => project.Name == "apiapp");
        ParameterResource dataGovSgApiKey = Assert.Single(
            appHost.Resources.OfType<ParameterResource>(),
            static parameter => parameter.Name == "data-gov-sg-api-key");
        Dictionary<string, object> apiEnvironment = await GatherEnvironmentAsync(
            api,
            appHost.ExecutionContext,
            cancellationTokenSource.Token);
        Assert.Same(dataGovSgApiKey, apiEnvironment["Availability__ApiKey"]);
    }

    [Fact]
    public async Task ProductionResourcesExposeWebRootAndApiHealth()
    {
        using CancellationTokenSource cancellationTokenSource = new(DefaultTimeout);
        IDistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.CarparkAvailability_AppHost>(
                s_testArguments,
                cancellationTokenSource.Token);

        ProjectResource api = Assert.IsType<ProjectResource>(
            Assert.Single(appHost.Resources, static resource => resource.Name == "apiapp"));
        ProjectResource web = Assert.IsType<ProjectResource>(
            Assert.Single(appHost.Resources, static resource => resource.Name == "webapp"));

        appHost.CreateResourceBuilder(api)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Production")
            .WithEnvironment("Availability__BaseUrl", "https://127.0.0.1:1/")
            .WithEnvironment("Availability__RequestTimeout", "00:00:01");
        appHost.CreateResourceBuilder(web)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Production");

        await using DistributedApplication app = await appHost
            .BuildAsync(cancellationTokenSource.Token)
            .WaitAsync(DefaultTimeout, cancellationTokenSource.Token);
        await app.StartAsync(cancellationTokenSource.Token)
            .WaitAsync(DefaultTimeout, cancellationTokenSource.Token);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("apiapp", cancellationTokenSource.Token)
            .WaitAsync(DefaultTimeout, cancellationTokenSource.Token);
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("webapp", cancellationTokenSource.Token)
            .WaitAsync(DefaultTimeout, cancellationTokenSource.Token);

        using HttpClient apiClient = app.CreateHttpClient("apiapp");
        using HttpResponseMessage healthResponse =
            await apiClient.GetAsync("/health", cancellationTokenSource.Token);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        using HttpClient webClient = app.CreateHttpClient("webapp");
        using HttpResponseMessage rootResponse =
            await webClient.GetAsync("/", cancellationTokenSource.Token);
        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);
    }

    private static void AssertProbes(ProjectResource resource)
    {
        EndpointProbeAnnotation[] probes = [.. resource.Annotations.OfType<EndpointProbeAnnotation>()];

        Assert.Collection(
            probes.OrderBy(static probe => probe.Type),
            static probe =>
            {
                Assert.Equal(ProbeType.Readiness, probe.Type);
                Assert.Equal("/health", probe.Path);
            },
            static probe =>
            {
                Assert.Equal(ProbeType.Liveness, probe.Type);
                Assert.Equal("/alive", probe.Path);
            });
    }

    private static async Task<Dictionary<string, object>> GatherEnvironmentAsync(
        ProjectResource resource,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object> environment = [];
        EnvironmentCallbackContext context = new(
            executionContext,
            resource,
            environment,
            cancellationToken);

        foreach (EnvironmentCallbackAnnotation annotation in
            resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        return environment;
    }
}
