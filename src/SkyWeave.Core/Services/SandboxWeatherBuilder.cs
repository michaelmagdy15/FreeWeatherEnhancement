using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

/// <summary>
/// Builds a fully compliant WeatherState from a SandboxWeatherScenario.
/// Synthesizes MSL/AGL datums, synthetic METAR strings, vertical cloud decks, winds aloft, and hazards.
/// </summary>
public static class SandboxWeatherBuilder
{
    private const double MetersToFeet = 3.28084;
    private const double FeetToMeters = 0.3048;

    public static WeatherState BuildWeatherState(
        SandboxWeatherScenario scenario,
        double latitude,
        double longitude,
        double aircraftAltitudeFeet,
        double stationElevationMeters = 0,
        string stationId = "SAND")
    {
        var now = DateTime.UtcNow;
        var cloudLayers = new List<CloudLayer>();

        // 1. Build Cloud Layers
        double? ceilingFeet = null;
        foreach (var deck in scenario.CloudDecks.OrderBy(d => d.BaseFeetAgl))
        {
            var baseMsl = stationElevationMeters + (deck.BaseFeetAgl * FeetToMeters);
            var topMsl = stationElevationMeters + (deck.TopFeetAgl * FeetToMeters);

            cloudLayers.Add(new CloudLayer
            {
                BaseFeetAgl = deck.BaseFeetAgl,
                TopFeetAgl = deck.TopFeetAgl,
                BaseMeters = baseMsl,
                TopMeters = topMsl,
                CoveragePercent = Math.Clamp(deck.CoveragePercent, 0, 100),
                Type = deck.Type,
                Density = Math.Clamp(deck.Density, 0.0, 1.0),
                Scattering = Math.Clamp(deck.Scattering, 0.0, 1.0)
            });

            if (ceilingFeet == null && deck.CoveragePercent >= 50.0)
            {
                ceilingFeet = deck.BaseFeetAgl;
            }
        }

        // 2. Flight Category
        var flightCat = DetermineFlightCategory(ceilingFeet, scenario.VisibilityMeters);

        // 3. Build Wind Layers
        var windLayers = new List<WindLayer>();

        // Ground / Surface layer anchored to station elevation
        windLayers.Add(new WindLayer
        {
            AltitudeFeet = stationElevationMeters * MetersToFeet,
            AltitudeMeters = stationElevationMeters,
            DirectionDegrees = scenario.SurfaceWindDirection,
            SpeedKnots = scenario.SurfaceWindSpeedKnots,
            GustSpeedKnots = scenario.SurfaceWindGustKnots,
            TemperatureCelsius = scenario.TemperatureCelsius,
            IsSurfaceLayer = true
        });

        // Aloft layers
        foreach (var aloft in scenario.WindsAloft)
        {
            var altMslFeet = aloft.AltitudeFeet;
            if (altMslFeet <= stationElevationMeters * MetersToFeet)
                continue; // Skip layers below terrain

            windLayers.Add(new WindLayer
            {
                AltitudeFeet = altMslFeet,
                AltitudeMeters = altMslFeet * FeetToMeters,
                DirectionDegrees = aloft.DirectionDegrees,
                SpeedKnots = aloft.SpeedKnots,
                TemperatureCelsius = aloft.TemperatureCelsius,
                IsSurfaceLayer = false
            });
        }

        windLayers = windLayers.OrderBy(w => w.AltitudeFeet).ToList();

        // 4. Hazards
        var hazards = new List<WeatherHazard>();
        if (scenario.TurbulenceIntensity > 0.15)
        {
            hazards.Add(new WeatherHazard
            {
                Type = HazardType.TurbulenceSigmet,
                Severity = Math.Clamp(scenario.TurbulenceIntensity, 0.0, 1.0),
                Description = $"{scenario.Name} - Turbulence ({scenario.TurbulenceIntensity:P0})"
            });
        }

        if (scenario.IcingSeverity > 0.15)
        {
            hazards.Add(new WeatherHazard
            {
                Type = HazardType.IcingSigmet,
                Severity = Math.Clamp(scenario.IcingSeverity, 0.0, 1.0),
                Description = $"{scenario.Name} - Structural Icing ({scenario.IcingSeverity:P0})"
            });
        }

        if (scenario.Thunderstorm)
        {
            hazards.Add(new WeatherHazard
            {
                Type = HazardType.ConvectiveSigmet,
                Severity = 0.95,
                Description = $"{scenario.Name} - Active Convective Cell & Lightning"
            });
        }

        // 5. Generate Synthetic METAR
        var rawMetar = GenerateSyntheticMetar(scenario, stationId, now, cloudLayers);

        // 6. Assemble WeatherState
        return new WeatherState
        {
            ObservationTime = now,
            StationId = stationId,
            RawMetar = rawMetar,
            Latitude = latitude,
            Longitude = longitude,
            TemperatureCelsius = scenario.TemperatureCelsius,
            DewpointCelsius = scenario.DewpointCelsius,
            PressureHpa = scenario.PressureHpa,
            AltimeterHpa = scenario.PressureHpa,
            VisibilityMeters = scenario.VisibilityMeters,
            FlightCategory = flightCat,
            WindDirectionDegrees = scenario.SurfaceWindDirection,
            WindSpeedKnots = scenario.SurfaceWindSpeedKnots,
            WindGustKnots = scenario.SurfaceWindGustKnots,
            CloudLayers = cloudLayers,
            WindsAloft = windLayers,
            Hazards = hazards,
            StormCells = scenario.Thunderstorm ? new List<StormCell>
            {
                new()
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    RadiusNm = 10.0,
                    Intensity = 0.9,
                    AltitudeFeet = 42000,
                    Type = CellType.Core
                }
            } : new List<StormCell>(),
            Precipitation = scenario.Precipitation,
            PrecipitationRate = scenario.PrecipitationRateMmHr,
            ThunderstormIntensity = scenario.Thunderstorm ? 0.9 : 0.0,
            CeilingFeet = ceilingFeet ?? 25000.0,
            IcingIndex = scenario.IcingSeverity,
            TurbulenceIndex = scenario.TurbulenceIntensity,
            ConvectiveAvailablePotentialEnergy = scenario.ConvectiveAvailablePotentialEnergy,
            LiftedIndex = scenario.LiftedIndex,
            SourceModelName = "SANDBOX",
            DataAgeMinutes = 0,
            IsSandbox = true,
            SandboxScenarioName = scenario.Name
        };
    }

    private static string DetermineFlightCategory(double? ceilingFeet, double visibilityMeters)
    {
        // LIFR: Ceiling < 500 ft and/or Visibility < 1 SM (1600m)
        if ((ceilingFeet.HasValue && ceilingFeet.Value < 500.0) || visibilityMeters < 1600.0)
            return "LIFR";

        // IFR: Ceiling 500 to < 1000 ft and/or Visibility 1 to < 3 SM (4800m)
        if ((ceilingFeet.HasValue && ceilingFeet.Value < 1000.0) || visibilityMeters < 4800.0)
            return "IFR";

        // MVFR: Ceiling 1000 to 3000 ft and/or Visibility 3 to 5 SM (8000m)
        if ((ceilingFeet.HasValue && ceilingFeet.Value <= 3000.0) || visibilityMeters <= 8000.0)
            return "MVFR";

        return "VFR";
    }

    public static string GenerateSyntheticMetar(
        SandboxWeatherScenario scenario,
        string stationId,
        DateTime timeUtc,
        List<CloudLayer> clouds)
    {
        var sb = new StringBuilder();
        sb.Append(FormattableString.Invariant($"{stationId} {timeUtc:ddHHmm}Z "));

        // Wind
        if (scenario.SurfaceWindSpeedKnots < 2.0 && (!scenario.SurfaceWindGustKnots.HasValue || scenario.SurfaceWindGustKnots < 2.0))
        {
            sb.Append("00000KT ");
        }
        else
        {
            var dir = ((int)Math.Round(scenario.SurfaceWindDirection)) % 360;
            var spd = (int)Math.Round(scenario.SurfaceWindSpeedKnots);
            if (scenario.SurfaceWindGustKnots.HasValue && scenario.SurfaceWindGustKnots.Value > scenario.SurfaceWindSpeedKnots)
            {
                var gst = (int)Math.Round(scenario.SurfaceWindGustKnots.Value);
                sb.Append(FormattableString.Invariant($"{dir:000}{spd:00}G{gst:00}KT "));
            }
            else
            {
                sb.Append(FormattableString.Invariant($"{dir:000}{spd:00}KT "));
            }
        }

        // Visibility
        if (scenario.VisibilityMeters >= 9999.0)
        {
            sb.Append("9999 ");
        }
        else
        {
            var visMeters = (int)Math.Round(scenario.VisibilityMeters);
            sb.Append(FormattableString.Invariant($"{visMeters:0000} "));
        }

        // Present Weather
        if (scenario.Thunderstorm)
        {
            sb.Append("TSRA ");
        }
        else if (scenario.Precipitation == PrecipitationType.Rain)
        {
            sb.Append(scenario.PrecipitationRateMmHr > 10.0 ? "+RA " : "RA ");
        }
        else if (scenario.Precipitation == PrecipitationType.FreezingRain)
        {
            sb.Append("FZRA ");
        }
        else if (scenario.Precipitation == PrecipitationType.Snow)
        {
            sb.Append("SN ");
        }
        else if (scenario.Precipitation == PrecipitationType.Mist)
        {
            sb.Append("BR ");
        }

        // Clouds
        if (clouds.Count == 0)
        {
            sb.Append("SKC ");
        }
        else
        {
            foreach (var c in clouds.Take(3))
            {
                string coverCode;
                if (c.CoveragePercent >= 87.5) coverCode = "OVC";
                else if (c.CoveragePercent >= 50.0) coverCode = "BKN";
                else if (c.CoveragePercent >= 25.0) coverCode = "SCT";
                else coverCode = "FEW";

                var baseHundFeet = (int)Math.Round(c.BaseFeetAgl / 100.0);
                sb.Append(FormattableString.Invariant($"{coverCode}{baseHundFeet:000} "));
            }
        }

        // Temperature / Dewpoint
        var tempStr = FormatTemp(scenario.TemperatureCelsius);
        var dewStr = FormatTemp(scenario.DewpointCelsius);
        sb.Append(FormattableString.Invariant($"{tempStr}/{dewStr} "));

        // Altimeter
        var qnh = (int)Math.Round(scenario.PressureHpa);
        sb.Append(FormattableString.Invariant($"Q{qnh:0000}"));

        return sb.ToString().Trim();
    }

    private static string FormatTemp(double tempC)
    {
        var rounded = (int)Math.Round(tempC);
        if (rounded < 0)
            return FormattableString.Invariant($"M{Math.Abs(rounded):00}");
        return FormattableString.Invariant($"{rounded:00}");
    }
}
