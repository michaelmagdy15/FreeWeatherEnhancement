using System.Text.RegularExpressions;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Decoders;

public partial class TafDecoder
{
    public TafData Decode(string rawTaf)
    {
        var taf = new TafData { RawText = rawTaf };

        var stationMatch = StationRegex().Match(rawTaf);
        if (stationMatch.Success)
            taf.StationId = stationMatch.Groups[1].Value;

        var validFromMatch = ValidFromRegex().Match(rawTaf);
        if (validFromMatch.Success)
            taf.ValidFrom = ParseTime(validFromMatch.Groups[1].Value);

        var validToMatch = ValidToRegex().Match(rawTaf);
        if (validToMatch.Success)
            taf.ValidTo = ParseTime(validToMatch.Groups[2].Value);

        var windMatch = WindRegex().Match(rawTaf);
        if (windMatch.Success)
        {
            taf.WindDirectionDegrees = double.Parse(windMatch.Groups[1].Value);
            taf.WindSpeedKnots = double.Parse(windMatch.Groups[2].Value);
            if (windMatch.Groups[3].Success && !string.IsNullOrEmpty(windMatch.Groups[3].Value))
                taf.WindGustKnots = double.Parse(windMatch.Groups[3].Value);
        }

        var visMatch = VisibilityRegex().Match(rawTaf);
        if (visMatch.Success)
        {
            var visValue = double.Parse(visMatch.Groups[1].Value);
            var unit = visMatch.Groups[2].Value;
            taf.VisibilityMeters = unit == "SM" ? visValue * 1609.344 : visValue * 1000;
        }

        var tempMatch = TemperatureRegex().Match(rawTaf);
        if (tempMatch.Success)
        {
            taf.MaxTemperatureCelsius = double.Parse(tempMatch.Groups[1].Value);
            taf.MinTemperatureCelsius = double.Parse(tempMatch.Groups[2].Value);
        }

        var clouds = CloudRegex().Matches(rawTaf);
        foreach (Match cloud in clouds)
        {
            var cloudType = new MetarCloud
            {
                Coverage = cloud.Groups[1].Value,
                BaseFeet = int.Parse(cloud.Groups[2].Value) * 100
            };
            taf.Clouds.Add(cloudType);
        }

        var wxMatches = WeatherPhenomenonRegex().Matches(rawTaf);
        foreach (Match wx in wxMatches)
        {
            taf.WeatherConditions.Add(wx.Value);
        }

        taf.FlightCategory = DetermineFlightCategory(taf);

        return taf;
    }

    public List<TafChangeGroup> DecodeChangeGroups(string rawTaf)
    {
        var groups = new List<TafChangeGroup>();
        var parts = rawTaf.Split(new[] { " BECMG ", " TEMPO ", " FM", " PROB" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts.Skip(1))
        {
            var group = new TafChangeGroup();

            if (part.StartsWith("FM"))
            {
                var fmMatch = FmRegex().Match(part);
                if (fmMatch.Success)
                {
                    group.Type = "FM";
                    group.ValidFrom = ParseTime(fmMatch.Groups[1].Value);
                }
            }
            else if (part.StartsWith("TEMPO"))
            {
                group.Type = "TEMPO";
            }
            else if (part.StartsWith("BECMG"))
            {
                group.Type = "BECMG";
            }
            else if (part.StartsWith("PROB"))
            {
                group.Type = "PROB";
                var probMatch = ProbRegex().Match(part);
                if (probMatch.Success)
                    group.Probability = int.Parse(probMatch.Groups[1].Value);
            }

            groups.Add(group);
        }

        return groups;
    }

    private string DetermineFlightCategory(TafData taf)
    {
        if (taf.Clouds.Any(c => c.Coverage == "OVC" || c.Coverage == "VV") && taf.Clouds.Any(c => c.BaseFeet <= 500))
            return "LIFR";
        if (taf.Clouds.Any(c => c.Coverage == "OVC" || c.Coverage == "VV") && taf.Clouds.Any(c => c.BaseFeet <= 1000))
            return "IFR";
        if (taf.Clouds.Any(c => (c.Coverage == "BKN" || c.Coverage == "OVC") && c.BaseFeet <= 3000))
            return "MVFR";
        return "VFR";
    }

    private DateTime ParseTime(string timeStr)
    {
        if (timeStr.Length == 6 && int.TryParse(timeStr[..2], out var day) &&
            int.TryParse(timeStr[2..4], out var hour) && int.TryParse(timeStr[4..6], out var min))
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, day, hour, min, 0, DateTimeKind.Utc);
        }
        return DateTime.UtcNow;
    }

    [GeneratedRegex(@"^(?:METAR\s+)?([A-Z]{4})\s")]
    private static partial Regex StationRegex();

    [GeneratedRegex(@"\s(\d{6})Z\s")]
    private static partial Regex ValidFromRegex();

    [GeneratedRegex(@"\s(\d{6})Z?\s*$")]
    private static partial Regex ValidToRegex();

    [GeneratedRegex(@"(\d{3})(\d{2,3})(G(\d{2,3}))?KT")]
    private static partial Regex WindRegex();

    [GeneratedRegex(@"(\d+)(SM|KM|M)")]
    private static partial Regex VisibilityRegex();

    [GeneratedRegex(@"T(\+?-?\d{2})(\d{2})/(\+?-?\d{2})(\d{2})")]
    private static partial Regex TemperatureRegex();

    [GeneratedRegex(@"(FEW|SCT|BKN|OVC|VV)(\d{3})")]
    private static partial Regex CloudRegex();

    [GeneratedRegex(@"(?:[-+VC]?)(RA|SN|TS|FG|BR|HZ|DZ|GR|GS|PL|FZRA|FZDZ|SHRA|SHSN|SCTSRA|TSGR)")]
    private static partial Regex WeatherPhenomenonRegex();

    [GeneratedRegex(@"FM(\d{6})")]
    private static partial Regex FmRegex();

    [GeneratedRegex(@"PROB(\d{2})")]
    private static partial Regex ProbRegex();
}

public class TafData
{
    public string StationId { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public double WindDirectionDegrees { get; set; }
    public double WindSpeedKnots { get; set; }
    public double? WindGustKnots { get; set; }
    public double VisibilityMeters { get; set; }
    public double MaxTemperatureCelsius { get; set; }
    public double MinTemperatureCelsius { get; set; }
    public List<MetarCloud> Clouds { get; set; } = new();
    public List<string> WeatherConditions { get; set; } = new();
    public string FlightCategory { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
}

public class TafChangeGroup
{
    public string Type { get; set; } = string.Empty;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int Probability { get; set; }
    public double? WindDirectionDegrees { get; set; }
    public double? WindSpeedKnots { get; set; }
    public double? VisibilityMeters { get; set; }
    public List<MetarCloud> Clouds { get; set; } = new();
    public List<string> WeatherConditions { get; set; } = new();
}
