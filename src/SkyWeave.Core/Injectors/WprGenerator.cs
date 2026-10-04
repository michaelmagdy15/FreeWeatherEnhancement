using System.Xml.Linq;
using SkyWeave.Core.Models;
using System.Globalization;

namespace SkyWeave.Core.Injectors;

public class WprGenerator
{
    private const double FEET_TO_METERS = 0.3048;

    public string GenerateWprXml(WeatherState state)
    {
        return GenerateWprXml(state, 0);
    }

    public string GenerateWprXml(WeatherState state, double turbulenceGustBoostKnots = 0)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("SimBase.Document",
                new XAttribute("Type", "WeatherPreset"),
                new XAttribute("version", "1,3"),
                new XElement("Descr", "AceXML Document"),
                GeneratePreset(state, turbulenceGustBoostKnots)
            )
        );

        return doc.ToString();
    }

    private XElement GeneratePreset(WeatherState state, double turbulenceGustBoostKnots)
    {
        var preset = new XElement("WeatherPreset.Preset",
            new XElement("Name", "SkyWeave"),
            new XElement("IsAltitudeAMGL", "False"),
            new XElement("MSLPressure",
                new XAttribute("Value", Format(state.PressureHpa * 100.0, "F0")),
                new XAttribute("Unit", "pa")),
            new XElement("MSLTemperature",
                new XAttribute("Value", Format(state.TemperatureCelsius + 273.15, "F2")),
                new XAttribute("Unit", "k")),
            new XElement("SnowCover",
                new XAttribute("Value", "0"),
                new XAttribute("Unit", "m")),
            new XElement("AerosolDensity",
                new XAttribute("Value", Format(state.AerosolDensity, "F3")),
                new XAttribute("Unit", "density factor")),
            new XElement("Precipitations",
                new XAttribute("Value", Format(state.PrecipitationRate, "F3")),
                new XAttribute("Unit", "mm/h")),
            new XElement("ThunderstormIntensity",
                new XAttribute("Value", Format(state.ThunderstormIntensity, "F3")),
                new XAttribute("Unit", "(0 - 1)"))
        );

        foreach (var cloud in state.CloudLayers.Take(24))
        {
            preset.Add(GenerateCloudLayer(cloud));
        }

        foreach (var wind in state.WindsAloft)
        {
            preset.Add(GenerateWindLayer(wind, turbulenceGustBoostKnots));
        }

        return preset;
    }

    private XElement GenerateCloudLayer(CloudLayer layer)
    {
        var cloudLayer = new XElement("CloudLayer",
            new XElement("CloudLayerDensity",
                new XAttribute("Value", Format(layer.Density, "F3")),
                new XAttribute("Unit", "(0 - 1)")),
            new XElement("CloudLayerAltitudeBot",
                new XAttribute("Value", Format(layer.BaseMeters, "F0")),
                new XAttribute("Unit", "m")),
            new XElement("CloudLayerAltitudeTop",
                new XAttribute("Value", Format(layer.TopMeters, "F0")),
                new XAttribute("Unit", "m")),
            new XElement("CloudLayerScattering",
                new XAttribute("Value", Format(layer.Scattering, "F3")),
                new XAttribute("Unit", "(0 - 1)"))
        );

        return cloudLayer;
    }

    private XElement GenerateWindLayer(WindLayer layer, double turbulenceGustBoostKnots)
    {
        var windLayer = new XElement("WindLayer",
            new XElement("WindLayerAltitude",
                new XAttribute("Value", Format(layer.AltitudeMeters, "F0")),
                new XAttribute("Unit", "m")),
            new XElement("WindLayerAngle",
                new XAttribute("Value", Format(layer.DirectionDegrees, "F0")),
                new XAttribute("Unit", "degrees")),
            new XElement("WindLayerSpeed",
                new XAttribute("Value", Format(layer.SpeedKnots, "F0")),
                new XAttribute("Unit", "knts"))
        );

        var gustSpeed = layer.GustSpeedKnots;
        var gustAngle = layer.GustDirectionDegrees ?? layer.DirectionDegrees;
        double gustIntervalSec = 10;
        double gustDurationSec = 2;

        // Preserve the station anchor at any elevation; retain the zero-altitude legacy guard.
        if (turbulenceGustBoostKnots > 0 && !layer.IsSurfaceLayer && layer.AltitudeFeet > 0)
        {
            if (layer.AltitudeFeet <= 6000)
            {
                // Surface thermals (fast, frequent bursts)
                gustSpeed = Math.Min(55, Math.Max(gustSpeed ?? 0, layer.SpeedKnots + turbulenceGustBoostKnots * 1.5));
                gustAngle = NormalizeAngle(layer.DirectionDegrees + (turbulenceGustBoostKnots > 10 ? 15 : -15));
                gustIntervalSec = 5;
                gustDurationSec = 3;
            }
            else if (layer.AltitudeFeet > 6000 && layer.AltitudeFeet < 10000)
            {
                // Microburst / Mid-level turbulence (tapered to avoid high altitude cruise yaw)
                gustSpeed = Math.Min(50, Math.Max(gustSpeed ?? 0, layer.SpeedKnots + turbulenceGustBoostKnots * 1.2));
                gustAngle = NormalizeAngle(layer.DirectionDegrees + 25);
                gustIntervalSec = 15;
                gustDurationSec = 4;
            }
        }

        // Boundary-layer gust tapering:
        // The observed METAR surface wind anchor (IsSurfaceLayer) represents ground level at any airport
        // elevation and is protected from altitude tapering. For aloft layers, suppress gusts at >= 10,000 ft
        // and taper from 3,000 to 10,000 ft to eliminate MSFS cruise yaw oscillations.
        double gustTaper = 1.0;
        if (!layer.IsSurfaceLayer)
        {
            if (layer.SpeedKnots < 5.0 || layer.AltitudeFeet >= 10000)
            {
                gustTaper = 0.0;
            }
            else if (layer.AltitudeFeet > 3000)
            {
                gustTaper = Math.Max(0.0, Math.Min(1.0, (10000.0 - layer.AltitudeFeet) / 7000.0));
            }
        }
        else if (layer.SpeedKnots < 5.0)
        {
            // Suppress gusts in calm/light air (< 5 kt)
            gustTaper = 0.0;
        }

        if (gustSpeed.HasValue)
        {
            gustSpeed = gustSpeed.Value * gustTaper;
        }

        if (gustSpeed.HasValue && gustSpeed.Value > 0.5)
        {
            windLayer.Add(new XElement("GustWave",
                new XElement("GustWaveDuration",
                    new XAttribute("Value", Format(gustDurationSec, "F0")),
                    new XAttribute("Unit", "sec")),
                new XElement("GustWaveInterval",
                    new XAttribute("Value", Format(gustIntervalSec, "F0")),
                    new XAttribute("Unit", "sec")),
                new XElement("GustWaveSpeed",
                    new XAttribute("Value", Format(gustSpeed.Value, "F0")),
                    new XAttribute("Unit", "knts")),
                new XElement("GustAngle",
                    new XAttribute("Value", Format(NormalizeAngle(gustAngle), "F0")),
                    new XAttribute("Unit", "degrees"))
            ));
        }

        return windLayer;
    }

    private static string Format(double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }
}
