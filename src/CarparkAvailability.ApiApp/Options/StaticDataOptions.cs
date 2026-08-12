namespace CarparkAvailability.ApiApp.Options;

public sealed class StaticDataOptions
{
    public const string SectionName = "StaticData";

    public string CsvPath { get; set; } = "Data/HDBCarparkInformation.csv";
}
