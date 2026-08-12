using System.Collections.Immutable;

namespace CarparkAvailability.ApiApp.Domain.CarParks;

public sealed class CarParkCatalog
{
    private readonly ImmutableDictionary<string, CarPark> _byNumber;

    public CarParkCatalog(IEnumerable<CarPark> carParks, int quarantinedRowCount)
    {
        ImmutableArray<CarPark> records = [.. carParks];
        Records = records;
        QuarantinedRowCount = quarantinedRowCount;
        _byNumber = records.ToImmutableDictionary(
            static carPark => carPark.CarParkNumber,
            StringComparer.OrdinalIgnoreCase);
    }

    public ImmutableArray<CarPark> Records { get; }

    public int QuarantinedRowCount { get; }

    public bool TryGet(string carParkNumber, out CarPark? carPark)
    {
        return _byNumber.TryGetValue(carParkNumber.Trim(), out carPark);
    }
}
