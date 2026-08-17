namespace SkyWeave.Core.Models;

public class AirportData
{
    public string IcaoId { get; set; } = string.Empty;
    public string IataId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int ElevationFeet { get; set; }
    public string Country { get; set; } = string.Empty;
}
