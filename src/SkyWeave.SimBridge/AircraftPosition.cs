namespace SkyWeave.SimBridge;

public class AircraftPosition
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AltitudeFeet { get; set; }
    public double HeadingDegrees { get; set; }
    public double GroundSpeedKnots { get; set; }
    public double VerticalSpeedFpm { get; set; }
    public DateTime Timestamp { get; set; }

    public AircraftPosition()
    {
        Timestamp = DateTime.UtcNow;
    }

    public AircraftPosition(double latitude, double longitude, double altitudeFeet)
    {
        Latitude = latitude;
        Longitude = longitude;
        AltitudeFeet = altitudeFeet;
        Timestamp = DateTime.UtcNow;
    }

    public double DistanceToNm(AircraftPosition other)
    {
        var R = 3440.065;
        var dLat = ToRadians(other.Latitude - Latitude);
        var dLon = ToRadians(other.Longitude - Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    public double BearingTo(AircraftPosition other)
    {
        var dLon = ToRadians(other.Longitude - Longitude);
        var lat1 = ToRadians(Latitude);
        var lat2 = ToRadians(other.Latitude);
        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        var bearing = ToDegrees(Math.Atan2(y, x));
        return (bearing + 360) % 360;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;
    private double ToDegrees(double radians) => radians * 180 / Math.PI;
}
