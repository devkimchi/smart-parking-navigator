#pragma warning disable ASPIREPROBES001

using Aspire.Hosting.ApplicationModel;
using Azure.Provisioning.AppContainers;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> googleMapsApiKey = builder.AddParameterFromConfiguration(
    "google-maps-api-key",
    "GoogleMaps:ApiKey",
    secret: true);
IResourceBuilder<ParameterResource> dataGovSgApiKey = builder.AddParameterFromConfiguration(
    "data-gov-sg-api-key",
    "DataGovSg:ApiKey",
    secret: true);

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureContainerAppEnvironment("environment");
}

IResourceBuilder<ProjectResource> api = builder
    .AddProject<Projects.CarparkAvailability_ApiApp>("apiapp")
    .WithEnvironment("Availability__ApiKey", dataGovSgApiKey)
    .WithHttpProbe(
        ProbeType.Liveness,
        "/alive",
        initialDelaySeconds: 5,
        periodSeconds: 30,
        timeoutSeconds: 3,
        failureThreshold: 3)
    .WithHttpProbe(
        ProbeType.Readiness,
        "/health",
        initialDelaySeconds: 5,
        periodSeconds: 10,
        timeoutSeconds: 3,
        failureThreshold: 3)
    .PublishAsAzureContainerApp(static (_, containerApp) => ConfigureSingleReplica(containerApp));

builder
    .AddProject<Projects.CarparkAvailability_WebApp>("webapp")
    .WithEnvironment("GoogleMaps__ApiKey", googleMapsApiKey)
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .WithHttpProbe(
        ProbeType.Liveness,
        "/alive",
        initialDelaySeconds: 5,
        periodSeconds: 30,
        timeoutSeconds: 3,
        failureThreshold: 3)
    .WithHttpProbe(
        ProbeType.Readiness,
        "/health",
        initialDelaySeconds: 5,
        periodSeconds: 10,
        timeoutSeconds: 3,
        failureThreshold: 3)
    .PublishAsAzureContainerApp(static (_, containerApp) => ConfigureSingleReplica(containerApp));

builder.Build().Run();

static void ConfigureSingleReplica(ContainerApp containerApp)
{
    ContainerAppTemplate template = containerApp.Template
        ?? throw new InvalidOperationException("The Azure Container App template was not initialized.");
    template.Scale = new ContainerAppScale
    {
        MinReplicas = 1,
        MaxReplicas = 1
    };
}
