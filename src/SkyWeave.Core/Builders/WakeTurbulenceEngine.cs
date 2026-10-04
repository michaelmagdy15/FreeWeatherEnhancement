using SkyWeave.Core.Models;

namespace SkyWeave.Core.Builders;

public class WakeTurbulenceEngine
{
    public const double MaxTrafficRangeNm = 15;
    public const double MinBehindDistanceNm = 0.5;
    public const double MaxBehindDistanceNm = 8;
    public const double AltitudeToleranceFeet = 1500;
    public const double CrossTrackMaxNm = 1.5;

    private const double EarthRadiusNm = 3440.065;
    private const double NmPerDegree = 60.0;
    private const double DriftCoefficient = 0.05;
    private const double DriftTimeFraction = 0.25;
    private const double AirportCorridorMaxDistanceNm = 12;
    private const double AirportCorridorMaxAltitudeFeet = 6000;
    private const double MinAirportTrafficDensity = 0.1;
    private const double WakeBaseOffsetFeet = 2000;
    private const double WakeTopOffsetFeet = 500;

    public List<TurbulenceLayer> CalculateWakeLayers(
        double aircraftLatitude,
        double aircraftLongitude,
        double aircraftAltitudeFeet,
        List<AircraftTraffic> traffic,
        List<WindLayer> windLayers,
        double nearestAirportLatitude,
        double nearestAirportLongitude,
        double nearestAirportDistanceNm,
        double airportTrafficDensity)
    {
        var layers = new List<TurbulenceLayer>();

        if (traffic != null && traffic.Count > 0)
        {
            layers.AddRange(CalculateTrafficWakeLayers(
                aircraftLatitude, aircraftLongitude, aircraftAltitudeFeet, traffic, windLayers));
        }

        var corridorActive = nearestAirportDistanceNm <= AirportCorridorMaxDistanceNm &&
                             aircraftAltitudeFeet <= AirportCorridorMaxAltitudeFeet;

        if (layers.Count == 0 && corridorActive && airportTrafficDensity > MinAirportTrafficDensity)
        {
            layers.AddRange(CalculateAirportCorridorLayers(windLayers, airportTrafficDensity));
        }

        return layers;
    }

    public double CalculateWakeIntensityFactor(AircraftWeightClass weightClass)
    {
        return weightClass switch
        {
            AircraftWeightClass.Super => 1.0,
            AircraftWeightClass.Heavy => 0.85,
            AircraftWeightClass.Medium => 0.55,
            AircraftWeightClass.Light => 0.25,
            _ => 0.4
        };
    }

    private List<TurbulenceLayer> CalculateTrafficWakeLayers(
        double aircraftLatitude,
        double aircraftLongitude,
        double aircraftAltitudeFeet,
        List<AircraftTraffic> traffic,
        List<WindLayer> windLayers)
    {
        var layers = new List<TurbulenceLayer>();

        var surfaceWindDirection = GetSurfaceWindDirection(windLayers);
        var surfaceWindSpeed = GetSurfaceWindSpeed(windLayers);
        var driftNm = DriftCoefficient * surfaceWindSpeed * DriftTimeFraction;
        var driftEastNm = driftNm * Math.Sin(ToRadians(surfaceWindDirection + 180));
        var driftNorthNm = driftNm * Math.Cos(ToRadians(surfaceWindDirection + 180));

        foreach (var aircraft in traffic)
        {
            var distanceNm = CalculateDistanceNm(
                aircraftLatitude, aircraftLongitude, aircraft.Latitude, aircraft.Longitude);
            aircraft.DistanceNm = Math.Round(distanceNm, 1);
            aircraft.AltitudeDeltaFeet = Math.Round(aircraft.AltitudeFeet - aircraftAltitudeFeet, 0);
            aircraft.RelativeBearingDegrees = Math.Round(
                CalculateBearing(aircraftLatitude, aircraftLongitude, aircraft.Latitude, aircraft.Longitude), 1);

            if (aircraft.OnGround) continue;
            if (distanceNm > MaxTrafficRangeNm) continue;

            var altitudeDifference = Math.Abs(aircraft.AltitudeFeet - aircraftAltitudeFeet);
            if (altitudeDifference > AltitudeToleranceFeet) continue;

            var behindNm = CalculateBehindDistanceNm(
                aircraftLatitude, aircraftLongitude, aircraft, driftEastNm, driftNorthNm, out var crossTrackNm);
            if (behindNm < MinBehindDistanceNm || behindNm > MaxBehindDistanceNm) continue;
            if (crossTrackNm > CrossTrackMaxNm) continue;

            aircraft.IsInWakeZone = true;

            var intensityFactor = CalculateWakeIntensityFactor(aircraft.WeightClass);
            var decay = CalculateWakeDecay(behindNm);
            var proximity = CalculateProximity(altitudeDifference, crossTrackNm);

            layers.Add(new TurbulenceLayer
            {
                BaseFeet = aircraft.AltitudeFeet - WakeBaseOffsetFeet,
                TopFeet = aircraft.AltitudeFeet + WakeTopOffsetFeet,
                Intensity = MapIntensity(intensityFactor * decay * proximity),
                Type = TurbulenceType.Wake
            });
        }

        return layers;
    }

    private static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var phi1 = lat1 * Math.PI / 180.0;
        var phi2 = lat2 * Math.PI / 180.0;
        var deltaLambda = (lon2 - lon1) * Math.PI / 180.0;
        var y = Math.Sin(deltaLambda) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(deltaLambda);
        var theta = Math.Atan2(y, x);
        return (theta * 180.0 / Math.PI + 360.0) % 360.0;
    }

    private List<TurbulenceLayer> CalculateAirportCorridorLayers(
        List<WindLayer> windLayers, double airportTrafficDensity)
    {
        var runwayHeading = RoundToNearestTen(GetSurfaceWindDirection(windLayers));
        var windFromDirection = NormalizeDegrees(runwayHeading + 180);

        var intensity = airportTrafficDensity switch
        {
            >= 0.65 => TurbulenceIntensity.Severe,
            >= 0.375 => TurbulenceIntensity.Moderate,
            > MinAirportTrafficDensity => TurbulenceIntensity.Light,
            _ => TurbulenceIntensity.None
        };

        return new List<TurbulenceLayer>
        {
            new()
            {
                BaseFeet = 1500,
                TopFeet = 3500,
                Intensity = intensity,
                Type = TurbulenceType.Wake
            },
            new()
            {
                BaseFeet = 2000,
                TopFeet = 4500,
                Intensity = intensity,
                Type = TurbulenceType.Wake
            }
        };
    }

    private double CalculateBehindDistanceNm(
        double aircraftLatitude,
        double aircraftLongitude,
        AircraftTraffic leader,
        double driftEastNm,
        double driftNorthNm,
        out double crossTrackNm)
    {
        var latCos = Math.Cos(ToRadians(leader.Latitude));
        var offsetEastNm = (aircraftLongitude - leader.Longitude) * NmPerDegree * latCos;
        var offsetNorthNm = (aircraftLatitude - leader.Latitude) * NmPerDegree;

        var shiftedEastNm = offsetEastNm - driftEastNm;
        var shiftedNorthNm = offsetNorthNm - driftNorthNm;

        var headingRadians = ToRadians(leader.HeadingDegrees);
        var trackEast = Math.Sin(headingRadians);
        var trackNorth = Math.Cos(headingRadians);

        // Distance user is trailing behind leader is the negative dot product of displacement with leader's track direction
        var behindNm = -(offsetEastNm * trackEast + offsetNorthNm * trackNorth);
        crossTrackNm = Math.Abs(shiftedEastNm * trackNorth - shiftedNorthNm * trackEast);

        return behindNm;
    }

    private double CalculateWakeDecay(double behindDistanceNm)
    {
        var t = (behindDistanceNm - MinBehindDistanceNm) / (MaxBehindDistanceNm - MinBehindDistanceNm);
        return Math.Max(0.4, 1.0 - t * 0.6);
    }

    private double CalculateProximity(double altitudeDifferenceFeet, double crossTrackNm)
    {
        var altitudeProximity = Math.Max(0.4, 1.0 - (altitudeDifferenceFeet / AltitudeToleranceFeet) * 0.5);
        var crossTrackProximity = Math.Max(0.6, 1.0 - (crossTrackNm / CrossTrackMaxNm) * 0.5);
        return altitudeProximity * crossTrackProximity;
    }

    private TurbulenceIntensity MapIntensity(double value)
    {
        if (value >= 0.75) return TurbulenceIntensity.Severe;
        if (value >= 0.5) return TurbulenceIntensity.Moderate;
        if (value >= 0.25) return TurbulenceIntensity.Light;
        return TurbulenceIntensity.None;
    }

    private double GetSurfaceWindSpeed(List<WindLayer> windLayers)
    {
        if (windLayers == null || windLayers.Count == 0) return 0;
        return windLayers.OrderBy(l => l.AltitudeFeet).First().SpeedKnots;
    }

    private double GetSurfaceWindDirection(List<WindLayer> windLayers)
    {
        if (windLayers == null || windLayers.Count == 0) return 0;
        return windLayers.OrderBy(l => l.AltitudeFeet).First().DirectionDegrees;
    }

    private double RoundToNearestTen(double degrees)
    {
        return Math.Round(degrees / 10.0) * 10.0;
    }

    private double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        if (normalized < 0) normalized += 360;
        return normalized;
    }

    private double CalculateDistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusNm * c;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;
}