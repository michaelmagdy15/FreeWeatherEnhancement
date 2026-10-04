namespace SkyWeave.Core.Models;

public enum ChartCategory
{
    General,
    AirportDiagram,
    Approach,
    Departure,
    Arrival,
    VisualApproach
}

public class AviationChart
{
    public string Icao { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ChartCategory Category { get; set; } = ChartCategory.General;
    public string Url { get; set; } = string.Empty;
    public string Provider { get; set; } = "Airmate (Free AIP)";
    public bool IsFree { get; set; } = true;

    public AviationChart() { }

    public AviationChart(string icao, string name, ChartCategory category, string url, string provider = "Airmate (Free AIP)", bool isFree = true)
    {
        Icao = icao;
        Name = name;
        Category = category;
        Url = url;
        Provider = provider;
        IsFree = isFree;
    }
}
