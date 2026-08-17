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

        var periodMatch = ChangeTimeRegex().Match(rawTaf);
        if (periodMatch.Success)
            taf.ValidTo = ParseTimeShort(periodMatch.Groups[2].Value);

        var windMatch = WindRegex().Match(rawTaf);
        if (windMatch.Success)
        {
            taf.WindDirectionDegrees = double.Parse(windMatch.Groups[1].Value);
            taf.WindSpeedKnots = double.Parse(windMatch.Groups[2].Value);
            if (windMatch.Groups[4].Success && !string.IsNullOrEmpty(windMatch.Groups[4].Value))
                taf.WindGustKnots = double.Parse(windMatch.Groups[4].Value);
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

        var initialSegment = ChangeGroupSplitRegex().Split(rawTaf)[0];
        var initialClouds = CloudRegex().Matches(initialSegment)
            .Select(c => new MetarCloud
            {
                Coverage = c.Groups[1].Value,
                BaseFeet = int.Parse(c.Groups[2].Value) * 100
            })
            .ToList();
        taf.FlightCategory = DetermineFlightCategory(initialClouds);

        return taf;
    }

    public List<TafChangeGroup> DecodeChangeGroups(string rawTaf)
    {
        var groups = new List<TafChangeGroup>();
        var segments = ChangeGroupSplitRegex().Split(rawTaf);

        foreach (var segment in segments.Skip(1))
        {
            var group = new TafChangeGroup();
            var validity = ChangeTimeRegex().Match(segment);
            var fmMatch = FmRegex().Match(segment);
            var probMatch = ProbRegex().Match(segment);

            if (fmMatch.Success)
                group.Type = "FM";
            else if (segment.StartsWith("TEMPO"))
                group.Type = "TEMPO";
            else if (segment.StartsWith("BECMG"))
                group.Type = "BECMG";
            else if (probMatch.Success)
                group.Type = "PROB";
            else
                continue;

            if (group.Type == "FM")
            {
                group.ValidFrom = ParseTime(fmMatch.Groups[1].Value);
                if (validity.Success)
                    group.ValidTo = ParseTimeShort(validity.Groups[2].Value);
            }
            else if (validity.Success)
            {
                group.ValidFrom = ParseTimeShort(validity.Groups[1].Value);
                group.ValidTo = ParseTimeShort(validity.Groups[2].Value);
            }

            if (probMatch.Success)
                group.Probability = int.Parse(probMatch.Groups[1].Value);

            var windMatch = WindRegex().Match(segment);
            if (windMatch.Success)
            {
                group.WindDirectionDegrees = double.Parse(windMatch.Groups[1].Value);
                group.WindSpeedKnots = double.Parse(windMatch.Groups[2].Value);
            }

            var visMatch = VisibilityRegex().Match(segment);
            if (visMatch.Success)
            {
                var visValue = double.Parse(visMatch.Groups[1].Value);
                var unit = visMatch.Groups[2].Value;
                group.VisibilityMeters = unit == "SM" ? visValue * 1609.344 : visValue * 1000;
            }

            foreach (Match cloud in CloudRegex().Matches(segment))
            {
                group.Clouds.Add(new MetarCloud
                {
                    Coverage = cloud.Groups[1].Value,
                    BaseFeet = int.Parse(cloud.Groups[2].Value) * 100
                });
            }

            foreach (Match wx in WeatherPhenomenonRegex().Matches(segment))
            {
                group.WeatherConditions.Add(wx.Value);
            }

            group.FlightCategory = DetermineFlightCategory(group.Clouds);
            groups.Add(group);
        }

        return groups;
    }

    private string DetermineFlightCategory(TafData taf)
    {
        return DetermineFlightCategory(taf.Clouds);
    }

    private string DetermineFlightCategory(List<MetarCloud> clouds)
    {
        if (clouds.Any(c => c.Coverage == "OVC" || c.Coverage == "VV") && clouds.Any(c => c.BaseFeet <= 500))
            return "LIFR";
        if (clouds.Any(c => c.Coverage == "OVC" || c.Coverage == "VV") && clouds.Any(c => c.BaseFeet <= 1000))
            return "IFR";
        if (clouds.Any(c => (c.Coverage == "BKN" || c.Coverage == "OVC") && c.BaseFeet <= 3000))
            return "MVFR";
        return "VFR";
    }

    private DateTime ParseTime(string timeStr)
    {
        if (timeStr.Length == 6 && int.TryParse(timeStr[..2], out var day) &&
            int.TryParse(timeStr[2..4], out var hour) && int.TryParse(timeStr[4..6], out var min))
        {
            return ToValidatedUtc(day, hour, min);
        }

        if (timeStr.Length == 4 && int.TryParse(timeStr[..2], out day) &&
            int.TryParse(timeStr[2..4], out hour))
        {
            return ToValidatedUtc(day, hour, 0);
        }

        return DateTime.UtcNow;
    }

    private DateTime ParseTimeShort(string timeStr)
    {
        if (timeStr.Length == 4 && int.TryParse(timeStr[..2], out var day) &&
            int.TryParse(timeStr[2..4], out var hour))
        {
            return ToValidatedUtc(day, hour, 0);
        }
        return DateTime.UtcNow;
    }

    private static DateTime ToValidatedUtc(int day, int hour, int minute)
    {
        var now = DateTime.UtcNow;
        var month = now.Month;
        var year = now.Year;
        if (hour == 24)
        {
            day += 1;
            hour = 0;
        }
        while (day > DateTime.DaysInMonth(year, month))
        {
            day -= DateTime.DaysInMonth(year, month);
            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }
        var candidate = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
        if (candidate > now.AddDays(3))
            candidate = candidate.AddMonths(-1);
        if (candidate < now.AddDays(-3))
            candidate = candidate.AddMonths(1);
        return candidate;
    }

    [GeneratedRegex(@"^(?:(?:TAF|METAR)\s+)?(?:AMD\s+)?([A-Z]{4})\s")]
    private static partial Regex StationRegex();

    [GeneratedRegex(@"\s(\d{6})Z\s")]
    private static partial Regex ValidFromRegex();

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

    [GeneratedRegex(@"(\d{4})/(\d{4})")]
    private static partial Regex ChangeTimeRegex();

    [GeneratedRegex(@"(?=\bFM\d{6}\b|\bTEMPO\b|\bBECMG\b|\bPROB\d{2}\b)")]
    private static partial Regex ChangeGroupSplitRegex();
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
    public string FlightCategory { get; set; } = string.Empty;
    public double? WindDirectionDegrees { get; set; }
    public double? WindSpeedKnots { get; set; }
    public double? VisibilityMeters { get; set; }
    public List<MetarCloud> Clouds { get; set; } = new();
    public List<string> WeatherConditions { get; set; } = new();
}
