using System.Collections.Immutable;
using System.Globalization;
using CarparkAvailability.ApiApp.Domain.Availability;
using CarparkAvailability.ApiApp.Infrastructure.DataGovSg.Generated;
using CarparkAvailability.ApiApp.Options;
using Microsoft.Extensions.Options;
using DataGovSgCarParkRecord = CarparkAvailability.ApiApp.Infrastructure.DataGovSg.Generated.Carpark_data;
using DataGovSgResponse = CarparkAvailability.ApiApp.Infrastructure.DataGovSg.Generated.Response;

namespace CarparkAvailability.ApiApp.Infrastructure.DataGovSg;

public sealed class DataGovSgAvailabilityClient(
    IDataGovSgApiClient apiClient,
    IOptions<AvailabilityOptions> options,
    TimeProvider timeProvider,
    ILogger<DataGovSgAvailabilityClient> logger) : IAvailabilityClient
{
    private static readonly TimeSpan s_singaporeOffset = TimeSpan.FromHours(8);
    private readonly AvailabilityOptions _options = options.Value;

    public async Task<AvailabilitySnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        DataGovSgResponse response = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? await apiClient.CarparkAvailabilityAsync(cancellationToken: cancellationToken)
            : await apiClient.CarparkAvailabilityAsync(
                x_api_key: _options.ApiKey,
                cancellationToken: cancellationToken);
        return Parse(response, timeProvider.GetUtcNow());
    }

    public AvailabilitySnapshot Parse(DataGovSgResponse response, DateTimeOffset retrievalTime)
    {
        if (response.Items is null || response.Items.Count == 0)
        {
            throw new InvalidDataException("The availability response contains no items.");
        }

        Dictionary<string, CarParkAvailability> records = new(StringComparer.OrdinalIgnoreCase);
        int validationErrors = 0;

        foreach (Generated.CarparkAvailability item in response.Items)
        {
            foreach (DataGovSgCarParkRecord recordElement in item.Carpark_data)
            {
                if (!TryParseRecord(recordElement, out CarParkAvailability? record, out int recordValidationErrors) ||
                    record is null)
                {
                    validationErrors += Math.Max(1, recordValidationErrors);
                    continue;
                }

                validationErrors += recordValidationErrors;
                if (!records.TryAdd(record.CarParkNumber, record))
                {
                    validationErrors++;
                    logger.LogWarning(
                        "Ignored duplicate live availability identifier {CarParkNumber}.",
                        record.CarParkNumber);
                }
            }
        }

        if (records.Count == 0)
        {
            throw new InvalidDataException("The availability response contains no valid car park records.");
        }

        DateTimeOffset sourceUpdateTime = records.Values.Max(static record => record.UpdateTime);
        return new AvailabilitySnapshot(
            sourceUpdateTime,
            retrievalTime,
            records.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
            validationErrors);
    }

    private static bool TryParseRecord(
        DataGovSgCarParkRecord element,
        out CarParkAvailability? record,
        out int validationErrors)
    {
        record = null;
        validationErrors = 0;
        if (string.IsNullOrWhiteSpace(element.CarparkNumber) ||
            string.IsNullOrWhiteSpace(element.UpdateDatetime) ||
            !TryParseUpdateTime(element.UpdateDatetime, out DateTimeOffset updateTime) ||
            element.CarparkInfo is null)
        {
            validationErrors++;
            return false;
        }

        List<LotAvailability> lots = [];
        foreach (DataGovSgCarparkLot lotElement in element.CarparkInfo)
        {
            if (!TryParseLot(lotElement, out LotAvailability? lot) || lot is null)
            {
                validationErrors++;
                continue;
            }

            lots.Add(lot);
        }

        if (lots.Count == 0)
        {
            validationErrors++;
            return false;
        }

        ImmutableArray<LotAvailability> normalizedLots = lots
            .GroupBy(static lot => lot.LotType)
            .Select(static group => new LotAvailability(
                group.Key,
                group.Sum(static lot => lot.TotalLots),
                group.Sum(static lot => lot.AvailableLots)))
            .ToImmutableArray();

        record = new CarParkAvailability(
            element.CarparkNumber.Trim(),
            updateTime.ToUniversalTime(),
            normalizedLots);
        return true;
    }

    private static bool TryParseUpdateTime(string text, out DateTimeOffset updateTime)
    {
        string value = text.Trim();
        int timeSeparatorIndex = value.IndexOf('T', StringComparison.Ordinal);
        int lastPlusIndex = value.LastIndexOf('+');
        int lastMinusIndex = value.LastIndexOf('-');
        bool hasExplicitOffset = value.EndsWith('Z') ||
            lastPlusIndex > timeSeparatorIndex ||
            lastMinusIndex > timeSeparatorIndex;

        if (hasExplicitOffset)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out updateTime);
        }

        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime localTime))
        {
            updateTime = default;
            return false;
        }

        updateTime = new DateTimeOffset(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), s_singaporeOffset);
        return true;
    }

    private static bool TryParseLot(DataGovSgCarparkLot element, out LotAvailability? lot)
    {
        lot = null;
        if (string.IsNullOrWhiteSpace(element.LotType) ||
            !int.TryParse(element.TotalLots, NumberStyles.None, CultureInfo.InvariantCulture, out int totalLots) ||
            !int.TryParse(element.LotsAvailable, NumberStyles.None, CultureInfo.InvariantCulture, out int availableLots) ||
            totalLots < 0 ||
            availableLots < 0 ||
            availableLots > totalLots)
        {
            return false;
        }

        lot = new LotAvailability(LotTypeMapper.Map(element.LotType), totalLots, availableLots);
        return true;
    }
}
