using CarparkAvailability.ApiApp.Domain.CarParks;

namespace CarparkAvailability.ApiApp.Services;

public sealed class CatalogInitializationService(CarParkCatalog catalog) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = catalog.Records.Length;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
