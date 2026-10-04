namespace SkyWeave.Core.Models;

public class IsobarLine
{
    public double PressureHpa { get; set; }
    public string SvgPath { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double LabelX { get; set; }
    public double LabelY { get; set; }
    public string StrokeColor { get; set; } = "#4099D8EE";
}

public class PressureCenter
{
    public string Type { get; set; } = "H"; // "H" or "L"
    public double PressureHpa { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string ColorHex { get; set; } = "#00D2D3";
    public string Label => $"{Type} {PressureHpa:F0}";
}

public class WindBarb
{
    public string StationId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double DirectionDegrees { get; set; }
    public double SpeedKnots { get; set; }
    public string SvgPath { get; set; } = string.Empty;
}

public class SynopticMapData
{
    public double CenterLatitude { get; set; }
    public double CenterLongitude { get; set; }
    public double RangeNm { get; set; }
    public List<IsobarLine> Isobars { get; set; } = new();
    public List<PressureCenter> Centers { get; set; } = new();
    public List<WindBarb> Barbs { get; set; } = new();
}
