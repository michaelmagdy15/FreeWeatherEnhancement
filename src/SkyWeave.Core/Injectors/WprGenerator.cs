using System.Xml.Linq;
using SkyWeave.Core.Models;

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
            new XElement("SimBase.Document",
                new XAttribute("Type", "WeatherPreset"),
                new XAttribute("version", "1,3"),
                new XElement("Descr", "SkyWeave Live Weather"),
                GeneratePreset(state, turbulenceGustBoostKnots)
            )
        );

        return doc.ToString();
    }

    private XElement GeneratePreset(WeatherState state, double turbulenceGustBoostKnots)
    {
        var preset = new XElement("WeatherPreset.Preset",
            new XElement("Name", "SkyWeave Live"),
            new XElement("IsAltitudeAMGL", "False"),
            new XElement("AerosolDensity",
                new XAttribute("Value", state.AerosolDensity.ToString("F3")),
                new XAttribute("Unit", "density factor")),
            new XElement("Precipitations",
                new XAttribute("Value", state.PrecipitationRate.ToString("F3")),
                new XAttribute("Unit", "mm/h")),
            new XElement("ThunderstormIntensity",
                new XAttribute("Value", state.ThunderstormIntensity.ToString("F3")),
                new XAttribute("Unit", "(0 - 1)"))
        );

        foreach (var cloud in state.CloudLayers)
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
                new XAttribute("Value", layer.Density.ToString("F3")),
                new XAttribute("Unit", "(0 - 1)")),
            new XElement("CloudLayerAltitudeBot",
                new XAttribute("Value", layer.BaseMeters.ToString("F0")),
                new XAttribute("Unit", "m")),
            new XElement("CloudLayerAltitudeTop",
                new XAttribute("Value", layer.TopMeters.ToString("F0")),
                new XAttribute("Unit", "m")),
            new XElement("CloudLayerScattering",
                new XAttribute("Value", layer.Scattering.ToString("F3")),
                new XAttribute("Unit", "(0 - 1)"))
        );

        return cloudLayer;
    }

    private XElement GenerateWindLayer(WindLayer layer, double turbulenceGustBoostKnots)
    {
        var windLayer = new XElement("WindLayer",
            new XElement("WindLayerAltitude",
                new XAttribute("Value", layer.AltitudeMeters.ToString("F0")),
                new XAttribute("Unit", "m")),
            new XElement("WindLayerAngle",
                new XAttribute("Value", layer.DirectionDegrees.ToString("F0")),
                new XAttribute("Unit", "degrees")),
            new XElement("WindLayerSpeed",
                new XAttribute("Value", layer.SpeedKnots.ToString("F0")),
                new XAttribute("Unit", "knts"))
        );

        if (layer.GustSpeedKnots.HasValue)
        {
            windLayer.Add(new XElement("WindLayerGusts",
                new XElement("WindLayerGustSpeed",
                    new XAttribute("Value", layer.GustSpeedKnots.Value.ToString("F0")),
                    new XAttribute("Unit", "knts")),
                new XElement("WindLayerGustAngle",
                    new XAttribute("Value", (layer.GustDirectionDegrees ?? layer.DirectionDegrees).ToString("F0")),
                    new XAttribute("Unit", "degrees"))
            ));
        }

        if (turbulenceGustBoostKnots > 0 && layer.AltitudeFeet <= 6000)
        {
            var gustSpeed = Math.Min(45, layer.SpeedKnots + turbulenceGustBoostKnots);
            var gustAngle = turbulenceGustBoostKnots > 15
                ? layer.DirectionDegrees + 10
                : layer.DirectionDegrees - 10;
            if (gustAngle < 0) gustAngle += 360;
            if (gustAngle >= 360) gustAngle -= 360;

            windLayer.Add(new XElement("WindLayerGusts",
                new XElement("WindLayerGustSpeed",
                    new XAttribute("Value", gustSpeed.ToString("F0")),
                    new XAttribute("Unit", "knts")),
                new XElement("WindLayerGustAngle",
                    new XAttribute("Value", gustAngle.ToString("F0")),
                    new XAttribute("Unit", "degrees"))
            ));
        }

        return windLayer;
    }
}
