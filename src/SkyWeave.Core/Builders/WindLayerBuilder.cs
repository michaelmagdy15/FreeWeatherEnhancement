using SkyWeave.Core.Models;

namespace SkyWeave.Core.Builders;

public class WindLayerBuilder
{
    private const double FEET_TO_METERS = WeatherUnits.FeetToMeters;

    public List<WindLayer> BuildWindLayers(MetarData metar, WindsAloftData? windsAloft)
        => BuildWindLayers(metar, windsAloft, 0);

    public List<WindLayer> BuildWindLayers(MetarData metar, WindsAloftData? windsAloft, double stationElevationFeet)
    {
        if (!double.IsFinite(stationElevationFeet)) throw new ArgumentOutOfRangeException(nameof(stationElevationFeet));
        var layers = new List<WindLayer>();
        int id = 1;

        layers.Add(new WindLayer
        {
            Id = id++,
            AltitudeMeters = Math.Max(0, stationElevationFeet * FEET_TO_METERS),
            AltitudeFeet = Math.Max(0, stationElevationFeet),
            IsSurfaceLayer = true,
            DirectionDegrees = metar.WindDirectionDegrees,
            SpeedKnots = metar.WindSpeedKnots,
            GustSpeedKnots = metar.WindGustKnots,
            GustDirectionDegrees = metar.WindGustKnots.HasValue ? metar.WindDirectionDegrees : null,
            TemperatureCelsius = metar.TemperatureCelsius
        });

        if (windsAloft?.PressureLevels != null)
        {
            foreach (var level in windsAloft.PressureLevels)
            {
                if (double.IsFinite(level.GeopotentialHeightMeters) &&
                    level.GeopotentialHeightMeters > Math.Max(0, stationElevationFeet) * FEET_TO_METERS)
                {
                    layers.Add(new WindLayer
                    {
                        Id = id++,
                        AltitudeMeters = level.GeopotentialHeightMeters,
                        AltitudeFeet = level.GeopotentialHeightMeters / FEET_TO_METERS,
                        DirectionDegrees = level.WindDirectionDegrees,
                        SpeedKnots = level.WindSpeedKnots,
                        TemperatureCelsius = level.TemperatureCelsius
                    });
                }
            }
        }

        return layers.OrderBy(l => l.AltitudeMeters).ToList();
    }

    public List<WindLayer> BuildWindLayersFromAltitudes(
        MetarData metar, 
        double[] altitudesFeet,
        double[]? temperatures = null,
        double[]? windDirections = null,
        double[]? windSpeeds = null)
    {
        var layers = new List<WindLayer>();
        int id = 1;

        layers.Add(new WindLayer
        {
            Id = id++,
            AltitudeMeters = 0,
            AltitudeFeet = 0,
            DirectionDegrees = metar.WindDirectionDegrees,
            SpeedKnots = metar.WindSpeedKnots,
            GustSpeedKnots = metar.WindGustKnots,
            TemperatureCelsius = metar.TemperatureCelsius
        });

        for (int i = 0; i < altitudesFeet.Length; i++)
        {
            var layer = new WindLayer
            {
                Id = id++,
                AltitudeFeet = altitudesFeet[i],
                AltitudeMeters = altitudesFeet[i] * FEET_TO_METERS
            };

            if (temperatures != null && i < temperatures.Length)
                layer.TemperatureCelsius = temperatures[i];

            if (windDirections != null && i < windDirections.Length)
                layer.DirectionDegrees = windDirections[i];

            if (windSpeeds != null && i < windSpeeds.Length)
                layer.SpeedKnots = windSpeeds[i];

            layers.Add(layer);
        }

        return layers;
    }
}
