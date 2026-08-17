namespace SkyWeave.Core.Models;

public class LightningStrike
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
    public double DistanceNm { get; set; }
    public int Polarity { get; set; }
}
