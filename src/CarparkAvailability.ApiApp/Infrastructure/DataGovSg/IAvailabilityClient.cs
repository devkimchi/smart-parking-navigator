using CarparkAvailability.ApiApp.Domain.Availability;

namespace CarparkAvailability.ApiApp.Infrastructure.DataGovSg;

public interface IAvailabilityClient
{
    Task<AvailabilitySnapshot> FetchAsync(CancellationToken cancellationToken);
}
