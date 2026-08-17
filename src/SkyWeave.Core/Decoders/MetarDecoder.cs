using System.Text.Json;
using System.Text.RegularExpressions;
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

    public MetarData DecodeRaw(string rawText)
    {
        var data = new MetarData { RawText = rawText };

        var station = Regex.Match(rawText, @"(?<![A-Z0-9])([A-Z]{4})(?=\s)")
            .Groups[1].Value;
        data.StationId = station;

        var obs = Regex.Match(rawText, @"\b(\d{2})(\d{2})(\d{2})Z\b");
        if (obs.Success)
        {
            var now = DateTime.UtcNow;
            var observed = new DateTime(now.Year, now.Month,
                int.Parse(obs.Groups[1].Value),
                int.Parse(obs.Groups[2].Value),
                int.Parse(obs.Groups[3].Value), 0, DateTimeKind.Utc);
            if (observed > now.AddHours(1))
                observed = observed.AddMonths(-1);
            data.ObservationTime = observed;
        }

        var wind = Regex.Match(rawText, @"\b(VRB|\d{3})(\d{2,3})(?:G(\d{2,3}))?KT\b");
        if (wind.Success)
        {
            data.WindDirectionDegrees = wind.Groups[1].Value == "VRB" ? 0 : double.Parse(wind.Groups[1].Value);
            data.WindSpeedKnots = double.Parse(wind.Groups[2].Value);
            if (wind.Groups[3].Success)
                data.WindGustKnots = double.Parse(wind.Groups[3].Value);
        }

        var vis = Regex.Match(rawText, @"\b(P)?(\d{1,2})(?:\s+(\d)/(\d))?SM\b");
        if (vis.Success)
        {
            var miles = double.Parse(vis.Groups[2].Value);
            if (vis.Groups[4].Success)
                miles += double.Parse(vis.Groups[3].Value) / double.Parse(vis.Groups[4].Value);
            if (vis.Groups[1].Success && miles < 10)
                miles = 10;
            data.VisibilityMeters = miles * 1609.344;
        }
        else
        {
            var visMeters = Regex.Match(rawText, @"(?<![QA])\b(\d{4})\b");
            if (visMeters.Success)
                data.VisibilityMeters = double.Parse(visMeters.Groups[1].Value);
        }

        var temp = Regex.Match(rawText, @"\b(M?\d{2})/(M?\d{2})\b");
        if (temp.Success)
        {
            data.TemperatureCelsius = ParseSigned(temp.Groups[1].Value);
            data.DewpointCelsius = ParseSigned(temp.Groups[2].Value);
        }

        var altim = Regex.Match(rawText, @"\bA(\d{4})\b");
        if (altim.Success)
            data.AltimeterHpa = int.Parse(altim.Groups[1].Value) * 33.8639 / 100.0;

        var qnh = Regex.Match(rawText, @"\bQ(\d{3,4})\b");
        if (qnh.Success)
            data.AltimeterHpa = double.Parse(qnh.Groups[1].Value);

        foreach (Match cloud in Regex.Matches(rawText, @"\b(FEW|SCT|BKN|OVC|VV)(\d{3})(?:CB|TCU)?\b"))
        {
            data.Clouds.Add(new MetarCloud
            {
                Coverage = cloud.Groups[1].Value,
                BaseFeet = cloud.Groups[1].Value == "VV" ? 0 : int.Parse(cloud.Groups[2].Value) * 100,
                Type = cloud.Groups[3].Success ? cloud.Groups[3].Value : string.Empty
            });
        }

        data.FlightCategory = ComputeFlightCategory(data);
        return data;
    }

    public static string ComputeFlightCategory(MetarData data)
    {
        var visMiles = data.VisibilityMeters > 0 ? data.VisibilityMeters / 1609.344 : 10;
        var ceiling = data.Clouds
            .Where(c => c.Coverage is "BKN" or "OVC" or "VV")
            .Select(c => c.BaseFeet)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        if (ceiling < 500 || visMiles < 1)
            return "LIFR";
        if (ceiling < 1000 || visMiles < 3)
            return "IFR";
        if (ceiling < 3000 || visMiles < 5)
            return "MVFR";
        return "VFR";
    }

    private static double ParseSigned(string value)
    {
        return value.StartsWith('M') ? -double.Parse(value[1..]) : double.Parse(value);
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
