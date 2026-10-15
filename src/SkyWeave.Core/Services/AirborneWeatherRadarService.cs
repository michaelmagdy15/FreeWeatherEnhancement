using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public enum AirborneRadarMode
{
    Wx = 0,
    WxPlusTurb = 1,
    TurbOnly = 2
}

public sealed record RadarSweepBin(
    double AzimuthOffsetDegrees,
    double RangeNm,
    double BeamAltitudeFeetMsl,
    double ReflectivityDbz,
    int ColorCode, // 0=Black, 1=Green, 2=Yellow, 3=Red, 4=Magenta (Turbulence)
    bool HasDopplerTurbulence);

public sealed record VerticalProfileColumn(
    double DistanceNm,
    double FreezingLevelFeet,
    IReadOnlyList<VerticalHazardBand> Bands);

public sealed record VerticalHazardBand(
    string Kind, // "CLOUD", "CB", "ICING", "TURB"
    double BaseFeetMsl,
    double TopFeetMsl,
    double Intensity,
    string Label);

public sealed record AirborneRadarScanResult(
    AirborneRadarMode Mode,
    double TiltDegrees,
    double GainDb,
    double RangeNm,
    double AircraftAltitudeFeet,
    double AircraftHeadingDegrees,
    int MaxReturnColorCode,
    string TiltAdvisory,
    IReadOnlyList<RadarSweepBin> SweepBins,
    IReadOnlyList<VerticalProfileColumn> VerticalProfile);

/// <summary>
/// Active Sky Next / Collins WXR-2100 Airborne Weather Radar & Vertical Weather Profile (VWP) engine.
/// Models 3D radar beam geometry (antenna tilt -15..+15 deg + Earth curvature), bright-band attenuation,
/// reflectivity color returns (Green/Yellow/Red), and Doppler WX+T turbulence detection (Magenta).
/// </summary>
public class AirborneWeatherRadarService
{
    public const double MinTiltDegrees = -15.0;
    public const double MaxTiltDegrees = 15.0;

    public AirborneRadarScanResult ComputeRadarScan(
        WeatherState? weather,
        double aircraftLatitude,
        double aircraftLongitude,
        double aircraftAltitudeFeet,
        double aircraftHeadingDegrees,
        double tiltDegrees = 0.0,
        double rangeNm = 80.0,
        double gainDb = 0.0,
        AirborneRadarMode mode = AirborneRadarMode.WxPlusTurb)
    {
        var clampedTilt = Math.Clamp(tiltDegrees, MinTiltDegrees, MaxTiltDegrees);
        var clampedRange = Math.Clamp(rangeNm, 10.0, 320.0);
        var clampedGain = Math.Clamp(gainDb, -12.0, 12.0);
        var normHeading = ((aircraftHeadingDegrees % 360.0) + 360.0) % 360.0;

        if (weather == null)
        {
            return new AirborneRadarScanResult(
                mode,
                clampedTilt,
                clampedGain,
                clampedRange,
                aircraftAltitudeFeet,
                normHeading,
                0,
                "NO WX DATA",
                Array.Empty<RadarSweepBin>(),
                Array.Empty<VerticalProfileColumn>());
        }

        var bins = new List<RadarSweepBin>();
        var maxColor = 0;

        // 120-degree forward sector (-60 to +60 deg in 10-deg steps), 8 range gates
        const int azimuthSteps = 13; // -60, -50, ..., +60
        const int rangeGates = 8;
        var tiltRad = clampedTilt * Math.PI / 180.0;
        var tanTilt = Math.Tan(tiltRad);

        for (int azIdx = 0; azIdx < azimuthSteps; azIdx++)
        {
            var azOffset = -60.0 + (azIdx * 10.0);
            var worldAzimuth = (normHeading + azOffset + 360.0) % 360.0;

            for (int rIdx = 1; rIdx <= rangeGates; rIdx++)
            {
                var gateRangeNm = clampedRange * ((double)rIdx / rangeGates);

                // Beam altitude MSL including Earth curvature drop (~0.667 * R_nm^2 ft)
                var beamAltFeet = aircraftAltitudeFeet
                    + (gateRangeNm * 6076.12 * tanTilt)
                    + (0.667 * gateRangeNm * gateRangeNm);

                var (sampleLat, sampleLon) = ProjectPoint(aircraftLatitude, aircraftLongitude, worldAzimuth, gateRangeNm);
                var (dbz, hasTurb) = SampleReflectivityAndTurbulence(
                    weather,
                    sampleLat,
                    sampleLon,
                    beamAltFeet,
                    gateRangeNm,
                    azOffset,
                    clampedGain);

                var colorCode = DetermineColorCode(dbz, hasTurb, mode, gateRangeNm);
                if (colorCode > maxColor) maxColor = colorCode;

                if (colorCode > 0)
                {
                    bins.Add(new RadarSweepBin(
                        AzimuthOffsetDegrees: azOffset,
                        RangeNm: Math.Round(gateRangeNm, 1),
                        BeamAltitudeFeetMsl: Math.Round(beamAltFeet, 0),
                        ReflectivityDbz: Math.Round(dbz, 1),
                        ColorCode: colorCode,
                        HasDopplerTurbulence: hasTurb));
                }
            }
        }

        var vwp = BuildVerticalProfile(weather, aircraftLatitude, aircraftLongitude, normHeading, clampedRange);
        var tiltAdvisory = BuildTiltAdvisory(aircraftAltitudeFeet, clampedTilt, clampedRange, maxColor);

        return new AirborneRadarScanResult(
            Mode: mode,
            TiltDegrees: clampedTilt,
            GainDb: clampedGain,
            RangeNm: clampedRange,
            AircraftAltitudeFeet: aircraftAltitudeFeet,
            AircraftHeadingDegrees: normHeading,
            MaxReturnColorCode: maxColor,
            TiltAdvisory: tiltAdvisory,
            SweepBins: bins,
            VerticalProfile: vwp);
    }

    private static (double Dbz, bool HasTurbulence) SampleReflectivityAndTurbulence(
        WeatherState weather,
        double sampleLat,
        double sampleLon,
        double beamAltFeet,
        double gateRangeNm,
        double azOffsetDeg,
        double gainDb)
    {
        if (beamAltFeet < 0 || beamAltFeet > 58000)
            return (0, false);

        double maxDbz = 0;
        bool hasTurb = false;

        // 1. Evaluate active StormCells in 3D space
        foreach (var cell in weather.StormCells)
        {
            var distToCellNm = StationFinder.CalculateDistance(sampleLat, sampleLon, cell.Latitude, cell.Longitude);
            var cellRadiusNm = Math.Max(3.5, cell.RadiusNm);
            if (distToCellNm > cellRadiusNm * 1.35)
                continue;

            var cellBaseFeet = 1500.0;
            var cellTopFeet = Math.Max(24000.0, cell.AltitudeFeet + 12000.0);
            if (beamAltFeet < cellBaseFeet || beamAltFeet > cellTopFeet)
                continue;

            var radialFactor = Math.Clamp(1.0 - (distToCellNm / (cellRadiusNm * 1.35)), 0.0, 1.0);
            // Bright-band peak reflectivity near freezing level, tapering above 25,000 ft (glaciated ice crystals)
            var verticalFactor = beamAltFeet <= weather.FreezingLevelFeet + 4000
                ? 1.0
                : Math.Clamp(1.0 - ((beamAltFeet - (weather.FreezingLevelFeet + 4000)) / 32000.0), 0.35, 1.0);

            var cellDbz = (22.0 + (cell.Intensity * 36.0)) * radialFactor * verticalFactor;
            if (cellDbz > maxDbz) maxDbz = cellDbz;

            if (cell.Intensity >= 0.60 && radialFactor >= 0.45 && gateRangeNm <= 45.0)
            {
                hasTurb = true;
            }
        }

        // 2. Evaluate CB/TCU/precipitating cloud layers intercepted by the beam
        foreach (var cloud in weather.CloudLayers)
        {
            var baseFt = cloud.BaseMeters / WeatherUnits.FeetToMeters;
            var topFt = cloud.TopMeters / WeatherUnits.FeetToMeters;
            if (beamAltFeet < baseFt || beamAltFeet > topFt)
                continue;

            double cloudDbz = 0;
            if (cloud.Type == CloudType.CB || cloud.IsConvective)
            {
                // Deterministic pseudo-cellular spatial structure across azimuth/range
                var cellPattern = 0.65 + 0.35 * Math.Cos((azOffsetDeg * 0.09) + (gateRangeNm * 0.14));
                cloudDbz = (32.0 + (weather.ThunderstormIntensity * 22.0)) * cellPattern;
                if (weather.ThunderstormIntensity >= 0.5 && gateRangeNm <= 40.0)
                    hasTurb = true;
            }
            else if (cloud.Type == CloudType.TCU)
            {
                cloudDbz = 26.0 + (cloud.Density * 12.0);
            }
            else if (weather.PrecipitationRate > 0.2 && cloud.CoveragePercent >= 0.5)
            {
                cloudDbz = Math.Clamp(15.0 + (weather.PrecipitationRate * 3.2), 16.0, 42.0);
            }

            if (cloudDbz > maxDbz) maxDbz = cloudDbz;
        }

        // 3. Check if beam intersects a Moderate/Severe TurbulenceLayer within Doppler range (<= 40 NM)
        if (gateRangeNm <= 40.0 && maxDbz >= 15.0)
        {
            foreach (var turb in weather.TurbulenceLayers)
            {
                if (turb.Intensity >= TurbulenceIntensity.Moderate &&
                    beamAltFeet >= turb.BaseFeet &&
                    beamAltFeet <= turb.TopFeet)
                {
                    hasTurb = true;
                    break;
                }
            }
        }

        if (maxDbz > 0)
        {
            maxDbz = Math.Max(0, maxDbz + gainDb);
        }

        return (maxDbz, hasTurb);
    }

    public static int DetermineColorCode(double dbz, bool hasTurbulence, AirborneRadarMode mode, double rangeNm)
    {
        // Doppler turbulence is only valid within 40 NM on real Collins/Honeywell WXR units
        if (mode == AirborneRadarMode.TurbOnly)
        {
            return (hasTurbulence && rangeNm <= 45.0) ? 4 : 0;
        }

        if (mode == AirborneRadarMode.WxPlusTurb && hasTurbulence && rangeNm <= 45.0)
        {
            return 4; // Magenta
        }

        if (dbz >= 42.0) return 3; // Red (Heavy)
        if (dbz >= 30.0) return 2; // Yellow (Moderate)
        if (dbz >= 18.0) return 1; // Green (Light)
        return 0;
    }

    private static IReadOnlyList<VerticalProfileColumn> BuildVerticalProfile(
        WeatherState weather,
        double lat,
        double lon,
        double headingDeg,
        double maxRangeNm)
    {
        const int columns = 8;
        var result = new List<VerticalProfileColumn>(columns);

        for (int i = 1; i <= columns; i++)
        {
            var distNm = Math.Round(maxRangeNm * ((double)i / columns), 1);
            var bands = new List<VerticalHazardBand>();

            foreach (var cloud in weather.CloudLayers)
            {
                var baseFt = Math.Round(cloud.BaseMeters / WeatherUnits.FeetToMeters, 0);
                var topFt = Math.Round(cloud.TopMeters / WeatherUnits.FeetToMeters, 0);
                var kind = (cloud.Type == CloudType.CB || cloud.IsConvective) ? "CB" : "CLOUD";
                bands.Add(new VerticalHazardBand(
                    kind,
                    baseFt,
                    topFt,
                    Math.Clamp(cloud.CoveragePercent, 0.1, 1.0),
                    $"{cloud.Type} {baseFt / 100:F0}"));
            }

            foreach (var icing in weather.IcingLayers)
            {
                if (icing.Severity == IcingSeverity.None) continue;
                bands.Add(new VerticalHazardBand(
                    "ICING",
                    Math.Round(icing.BaseFeet, 0),
                    Math.Round(icing.TopFeet, 0),
                    Math.Clamp((int)icing.Severity / 4.0, 0.25, 1.0),
                    $"ICE {icing.Severity}"));
            }

            foreach (var turb in weather.TurbulenceLayers)
            {
                if (turb.Intensity == TurbulenceIntensity.None) continue;
                bands.Add(new VerticalHazardBand(
                    "TURB",
                    Math.Round(turb.BaseFeet, 0),
                    Math.Round(turb.TopFeet, 0),
                    Math.Clamp((int)turb.Intensity / 4.0, 0.25, 1.0),
                    $"CAT {turb.Intensity}"));
            }

            result.Add(new VerticalProfileColumn(
                DistanceNm: distNm,
                FreezingLevelFeet: Math.Round(weather.FreezingLevelFeet, 0),
                Bands: bands));
        }

        return result;
    }

    private static string BuildTiltAdvisory(double altFeet, double tiltDeg, double rangeNm, int maxColor)
    {
        var recommendedTilt = altFeet switch
        {
            > 28000 => -2.5,
            > 18000 => -1.5,
            > 10000 => 0.0,
            _ => 2.0
        };

        var returnLabel = maxColor switch
        {
            4 => "MAGENTA DOPPLER TURB DETECTED",
            3 => "RED HEAVY CONVECTIVE CORE",
            2 => "YELLOW MODERATE PRECIP",
            1 => "GREEN LIGHT PRECIP",
            _ => "CLEAR PATH"
        };

        return $"{returnLabel} // OPTIMAL TILT {recommendedTilt:+0.0;-0.0;0.0}°";
    }

    private static (double Lat, double Lon) ProjectPoint(double lat, double lon, double bearingDeg, double distanceNm)
    {
        var angularDistance = distanceNm / 3440.065;
        var brng = bearingDeg * Math.PI / 180.0;
        var lat1 = lat * Math.PI / 180.0;
        var lon1 = lon * Math.PI / 180.0;

        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(angularDistance) +
                             Math.Cos(lat1) * Math.Sin(angularDistance) * Math.Cos(brng));
        var lon2 = lon1 + Math.Atan2(
            Math.Sin(brng) * Math.Sin(angularDistance) * Math.Cos(lat1),
            Math.Cos(angularDistance) - Math.Sin(lat1) * Math.Sin(lat2));

        return (lat2 * 180.0 / Math.PI, lon2 * 180.0 / Math.PI);
    }
}
