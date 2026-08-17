using System.Text.Json;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Decoders;

public class MetarDecoder
{
    public MetarData Decode(JsonElement metar)
    {
        var data = new MetarData
        {
            StationId = metar.GetProperty("icaoId").GetString() ?? string.Empty,
            RawText = metar.TryGetProperty("rawOb", out var raw) ? raw.GetString() ?? string.Empty : string.Empty,
            FlightCategory = metar.TryGetProperty("fltCat", out var cat) ? cat.GetString() ?? string.Empty : string.Empty
        };

        if (metar.TryGetProperty("temp", out var temp))
            data.TemperatureCelsius = ParseDouble(temp);

        if (metar.TryGetProperty("dewp", out var dewp))
            data.DewpointCelsius = ParseDouble(dewp);

        if (metar.TryGetProperty("wdir", out var wdir))
            data.WindDirectionDegrees = ParseDouble(wdir);

        if (metar.TryGetProperty("wspd", out var wspd))
            data.WindSpeedKnots = ParseDouble(wspd);

        if (metar.TryGetProperty("wgst", out var wgst) && wgst.ValueKind != JsonValueKind.Null)
            data.WindGustKnots = ParseDouble(wgst);

        if (metar.TryGetProperty("visib", out var visib))
            data.VisibilityMeters = ParseDouble(visib) * 1609.344;

        if (metar.TryGetProperty("altim", out var altim))
            data.AltimeterHpa = ParseDouble(altim);

        if (metar.TryGetProperty("clouds", out var clouds) && clouds.ValueKind == JsonValueKind.Array)
        {
            foreach (var cloud in clouds.EnumerateArray())
            {
                var cloudData = new MetarCloud
                {
                    Coverage = cloud.TryGetProperty("cover", out var cover) ? cover.GetString() ?? string.Empty : string.Empty,
                    BaseFeet = cloud.TryGetProperty("base", out var baseFeet) ? ParseInt32(baseFeet) : 0,
                    Type = cloud.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty
                };
                data.Clouds.Add(cloudData);
            }
        }

        if (metar.TryGetProperty("obsTime", out var obsTime))
        {
            data.ObservationTime = ParseObservationTime(obsTime);
        }

        return data;
    }

    private static DateTime ParseObservationTime(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return DateTimeOffset.FromUnixTimeSeconds(element.GetInt64()).UtcDateTime;

        if (element.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(element.GetString(), out var offset))
            return offset.UtcDateTime;

        return DateTime.MinValue;
    }

    private static double ParseDouble(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return double.TryParse(element.GetString(), out var val) ? val : 0;
        return element.GetDouble();
    }

    private static int ParseInt32(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return int.TryParse(element.GetString(), out var val) ? val : 0;
        return element.GetInt32();
    }
}
