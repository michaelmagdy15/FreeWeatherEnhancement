namespace SkyWeave.Core.Models;

public class CloudLayer
{
    public int Id { get; set; }
    /// <summary>Cloud base above mean sea level, in meters.</summary>
    public double BaseMeters { get; set; }
    /// <summary>Cloud top above mean sea level, in meters.</summary>
    public double TopMeters { get; set; }
    /// <summary>Height relative to the reporting station elevation, for briefing.</summary>
    public double BaseFeetAgl { get; set; }
    public double TopFeetAgl { get; set; }
    public CloudType Type { get; set; }
    public double Density { get; set; }
    public double Scattering { get; set; }
    public double CoveragePercent { get; set; }
    public bool IsConvective { get; set; }
    public double? ThunderstormIntensity { get; set; }
}

public enum CloudType
{
    FEW,
    SCT,
    BKN,
    OVC,
    CB,
    TCU,
    ST,
    NS
}
