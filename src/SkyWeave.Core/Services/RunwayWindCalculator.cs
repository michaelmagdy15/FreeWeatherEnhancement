using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public static class RunwayWindCalculator
{
    private static readonly Dictionary<string, List<RunwayInfo>> AirportRunwayCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        // Major North America Hubs
        ["KJFK"] = [
            new() { Identifier = "04L", HeadingDegrees = 44, LengthFeet = 12079 },
            new() { Identifier = "22R", HeadingDegrees = 224, LengthFeet = 12079 },
            new() { Identifier = "04R", HeadingDegrees = 44, LengthFeet = 8400 },
            new() { Identifier = "22L", HeadingDegrees = 224, LengthFeet = 8400 },
            new() { Identifier = "13L", HeadingDegrees = 134, LengthFeet = 14511 },
            new() { Identifier = "31R", HeadingDegrees = 314, LengthFeet = 14511 },
            new() { Identifier = "13R", HeadingDegrees = 134, LengthFeet = 10000 },
            new() { Identifier = "31L", HeadingDegrees = 314, LengthFeet = 10000 }
        ],
        ["KLAX"] = [
            new() { Identifier = "06L", HeadingDegrees = 71, LengthFeet = 8926 },
            new() { Identifier = "24R", HeadingDegrees = 251, LengthFeet = 8926 },
            new() { Identifier = "06R", HeadingDegrees = 71, LengthFeet = 10285 },
            new() { Identifier = "24L", HeadingDegrees = 251, LengthFeet = 10285 },
            new() { Identifier = "07L", HeadingDegrees = 71, LengthFeet = 12091 },
            new() { Identifier = "25R", HeadingDegrees = 251, LengthFeet = 12091 },
            new() { Identifier = "07R", HeadingDegrees = 71, LengthFeet = 11095 },
            new() { Identifier = "25L", HeadingDegrees = 251, LengthFeet = 11095 }
        ],
        ["KORD"] = [
            new() { Identifier = "09C", HeadingDegrees = 90, LengthFeet = 11260 },
            new() { Identifier = "27C", HeadingDegrees = 270, LengthFeet = 11260 },
            new() { Identifier = "09L", HeadingDegrees = 90, LengthFeet = 7500 },
            new() { Identifier = "27R", HeadingDegrees = 270, LengthFeet = 7500 },
            new() { Identifier = "09R", HeadingDegrees = 90, LengthFeet = 7500 },
            new() { Identifier = "27L", HeadingDegrees = 270, LengthFeet = 7500 },
            new() { Identifier = "10C", HeadingDegrees = 100, LengthFeet = 10800 },
            new() { Identifier = "28C", HeadingDegrees = 280, LengthFeet = 10800 },
            new() { Identifier = "10L", HeadingDegrees = 100, LengthFeet = 13000 },
            new() { Identifier = "28R", HeadingDegrees = 280, LengthFeet = 13000 },
            new() { Identifier = "10R", HeadingDegrees = 100, LengthFeet = 7500 },
            new() { Identifier = "28L", HeadingDegrees = 280, LengthFeet = 7500 }
        ],
        ["KATL"] = [
            new() { Identifier = "08L", HeadingDegrees = 95, LengthFeet = 9000 },
            new() { Identifier = "26R", HeadingDegrees = 275, LengthFeet = 9000 },
            new() { Identifier = "08R", HeadingDegrees = 95, LengthFeet = 9999 },
            new() { Identifier = "26L", HeadingDegrees = 275, LengthFeet = 9999 },
            new() { Identifier = "09L", HeadingDegrees = 95, LengthFeet = 12390 },
            new() { Identifier = "27R", HeadingDegrees = 275, LengthFeet = 12390 },
            new() { Identifier = "09R", HeadingDegrees = 95, LengthFeet = 9000 },
            new() { Identifier = "27L", HeadingDegrees = 275, LengthFeet = 9000 },
            new() { Identifier = "10", HeadingDegrees = 95, LengthFeet = 9000 },
            new() { Identifier = "28", HeadingDegrees = 275, LengthFeet = 9000 }
        ],
        ["KSFO"] = [
            new() { Identifier = "01L", HeadingDegrees = 14, LengthFeet = 7650 },
            new() { Identifier = "19R", HeadingDegrees = 194, LengthFeet = 7650 },
            new() { Identifier = "01R", HeadingDegrees = 14, LengthFeet = 8650 },
            new() { Identifier = "19L", HeadingDegrees = 194, LengthFeet = 8650 },
            new() { Identifier = "10L", HeadingDegrees = 104, LengthFeet = 11870 },
            new() { Identifier = "28R", HeadingDegrees = 284, LengthFeet = 11870 },
            new() { Identifier = "10R", HeadingDegrees = 104, LengthFeet = 11381 },
            new() { Identifier = "28L", HeadingDegrees = 284, LengthFeet = 11381 }
        ],
        ["KDEN"] = [
            new() { Identifier = "07", HeadingDegrees = 83, LengthFeet = 12000 },
            new() { Identifier = "25", HeadingDegrees = 263, LengthFeet = 12000 },
            new() { Identifier = "08", HeadingDegrees = 83, LengthFeet = 12000 },
            new() { Identifier = "26", HeadingDegrees = 263, LengthFeet = 12000 },
            new() { Identifier = "16L", HeadingDegrees = 173, LengthFeet = 12000 },
            new() { Identifier = "34R", HeadingDegrees = 353, LengthFeet = 12000 },
            new() { Identifier = "16R", HeadingDegrees = 173, LengthFeet = 16000 },
            new() { Identifier = "34L", HeadingDegrees = 353, LengthFeet = 16000 },
            new() { Identifier = "17L", HeadingDegrees = 173, LengthFeet = 12000 },
            new() { Identifier = "35R", HeadingDegrees = 353, LengthFeet = 12000 },
            new() { Identifier = "17R", HeadingDegrees = 173, LengthFeet = 12000 },
            new() { Identifier = "35L", HeadingDegrees = 353, LengthFeet = 12000 }
        ],
        ["KDFW"] = [
            new() { Identifier = "13L", HeadingDegrees = 133, LengthFeet = 9000 },
            new() { Identifier = "31R", HeadingDegrees = 313, LengthFeet = 9000 },
            new() { Identifier = "13R", HeadingDegrees = 133, LengthFeet = 9300 },
            new() { Identifier = "31L", HeadingDegrees = 313, LengthFeet = 9300 },
            new() { Identifier = "17C", HeadingDegrees = 175, LengthFeet = 13401 },
            new() { Identifier = "35C", HeadingDegrees = 355, LengthFeet = 13401 },
            new() { Identifier = "17L", HeadingDegrees = 175, LengthFeet = 8500 },
            new() { Identifier = "35R", HeadingDegrees = 355, LengthFeet = 8500 },
            new() { Identifier = "17R", HeadingDegrees = 175, LengthFeet = 13400 },
            new() { Identifier = "35L", HeadingDegrees = 355, LengthFeet = 13400 },
            new() { Identifier = "18L", HeadingDegrees = 175, LengthFeet = 13400 },
            new() { Identifier = "36R", HeadingDegrees = 355, LengthFeet = 13400 },
            new() { Identifier = "18R", HeadingDegrees = 175, LengthFeet = 13400 },
            new() { Identifier = "36L", HeadingDegrees = 355, LengthFeet = 13400 }
        ],
        ["KMIA"] = [
            new() { Identifier = "08L", HeadingDegrees = 92, LengthFeet = 8600 },
            new() { Identifier = "26R", HeadingDegrees = 272, LengthFeet = 8600 },
            new() { Identifier = "08R", HeadingDegrees = 92, LengthFeet = 10506 },
            new() { Identifier = "26L", HeadingDegrees = 272, LengthFeet = 10506 },
            new() { Identifier = "09", HeadingDegrees = 92, LengthFeet = 13016 },
            new() { Identifier = "27", HeadingDegrees = 272, LengthFeet = 13016 },
            new() { Identifier = "12", HeadingDegrees = 122, LengthFeet = 9354 },
            new() { Identifier = "30", HeadingDegrees = 302, LengthFeet = 9354 }
        ],
        ["CYYZ"] = [
            new() { Identifier = "05", HeadingDegrees = 57, LengthFeet = 11120 },
            new() { Identifier = "23", HeadingDegrees = 237, LengthFeet = 11120 },
            new() { Identifier = "06L", HeadingDegrees = 57, LengthFeet = 9694 },
            new() { Identifier = "24R", HeadingDegrees = 237, LengthFeet = 9694 },
            new() { Identifier = "06R", HeadingDegrees = 57, LengthFeet = 9000 },
            new() { Identifier = "24L", HeadingDegrees = 237, LengthFeet = 9000 },
            new() { Identifier = "15L", HeadingDegrees = 147, LengthFeet = 11050 },
            new() { Identifier = "33R", HeadingDegrees = 327, LengthFeet = 11050 },
            new() { Identifier = "15R", HeadingDegrees = 147, LengthFeet = 9070 },
            new() { Identifier = "33L", HeadingDegrees = 327, LengthFeet = 9070 }
        ],
        ["CYVR"] = [
            new() { Identifier = "08L", HeadingDegrees = 83, LengthFeet = 9940 },
            new() { Identifier = "26R", HeadingDegrees = 263, LengthFeet = 9940 },
            new() { Identifier = "08R", HeadingDegrees = 83, LengthFeet = 11500 },
            new() { Identifier = "26L", HeadingDegrees = 263, LengthFeet = 11500 },
            new() { Identifier = "13", HeadingDegrees = 134, LengthFeet = 7300 },
            new() { Identifier = "31", HeadingDegrees = 314, LengthFeet = 7300 }
        ],

        // Major European Hubs
        ["EGLL"] = [
            new() { Identifier = "09L", HeadingDegrees = 92, LengthFeet = 12799 },
            new() { Identifier = "27R", HeadingDegrees = 272, LengthFeet = 12799 },
            new() { Identifier = "09R", HeadingDegrees = 92, LengthFeet = 12008 },
            new() { Identifier = "27L", HeadingDegrees = 272, LengthFeet = 12008 }
        ],
        ["EGKK"] = [
            new() { Identifier = "08R", HeadingDegrees = 82, LengthFeet = 10879 },
            new() { Identifier = "26L", HeadingDegrees = 262, LengthFeet = 10879 },
            new() { Identifier = "08L", HeadingDegrees = 82, LengthFeet = 8415 },
            new() { Identifier = "26R", HeadingDegrees = 262, LengthFeet = 8415 }
        ],
        ["LFPG"] = [
            new() { Identifier = "08L", HeadingDegrees = 86, LengthFeet = 13829 },
            new() { Identifier = "26R", HeadingDegrees = 266, LengthFeet = 13829 },
            new() { Identifier = "08R", HeadingDegrees = 86, LengthFeet = 8858 },
            new() { Identifier = "26L", HeadingDegrees = 266, LengthFeet = 8858 },
            new() { Identifier = "09L", HeadingDegrees = 86, LengthFeet = 8858 },
            new() { Identifier = "27R", HeadingDegrees = 266, LengthFeet = 8858 },
            new() { Identifier = "09R", HeadingDegrees = 86, LengthFeet = 13780 },
            new() { Identifier = "27L", HeadingDegrees = 266, LengthFeet = 13780 }
        ],
        ["EDDF"] = [
            new() { Identifier = "07C", HeadingDegrees = 69, LengthFeet = 13123 },
            new() { Identifier = "25C", HeadingDegrees = 249, LengthFeet = 13123 },
            new() { Identifier = "07L", HeadingDegrees = 69, LengthFeet = 9186 },
            new() { Identifier = "25R", HeadingDegrees = 249, LengthFeet = 9186 },
            new() { Identifier = "07R", HeadingDegrees = 69, LengthFeet = 13123 },
            new() { Identifier = "25L", HeadingDegrees = 249, LengthFeet = 13123 },
            new() { Identifier = "18", HeadingDegrees = 179, LengthFeet = 13123 }
        ],
        ["EHAM"] = [
            new() { Identifier = "06", HeadingDegrees = 58, LengthFeet = 11483 },
            new() { Identifier = "24", HeadingDegrees = 238, LengthFeet = 11483 },
            new() { Identifier = "09", HeadingDegrees = 87, LengthFeet = 11329 },
            new() { Identifier = "27", HeadingDegrees = 267, LengthFeet = 11329 },
            new() { Identifier = "18C", HeadingDegrees = 183, LengthFeet = 10827 },
            new() { Identifier = "36C", HeadingDegrees = 3, LengthFeet = 10827 },
            new() { Identifier = "18L", HeadingDegrees = 183, LengthFeet = 11155 },
            new() { Identifier = "36R", HeadingDegrees = 3, LengthFeet = 11155 },
            new() { Identifier = "18R", HeadingDegrees = 183, LengthFeet = 12467 },
            new() { Identifier = "36L", HeadingDegrees = 3, LengthFeet = 12467 },
            new() { Identifier = "04", HeadingDegrees = 41, LengthFeet = 6608 },
            new() { Identifier = "22", HeadingDegrees = 221, LengthFeet = 6608 }
        ],
        ["EDDM"] = [
            new() { Identifier = "08L", HeadingDegrees = 83, LengthFeet = 13123 },
            new() { Identifier = "26R", HeadingDegrees = 263, LengthFeet = 13123 },
            new() { Identifier = "08R", HeadingDegrees = 83, LengthFeet = 13123 },
            new() { Identifier = "26L", HeadingDegrees = 263, LengthFeet = 13123 }
        ],
        ["LEMD"] = [
            new() { Identifier = "14L", HeadingDegrees = 145, LengthFeet = 11483 },
            new() { Identifier = "32R", HeadingDegrees = 325, LengthFeet = 11483 },
            new() { Identifier = "14R", HeadingDegrees = 145, LengthFeet = 13451 },
            new() { Identifier = "32L", HeadingDegrees = 325, LengthFeet = 13451 },
            new() { Identifier = "18L", HeadingDegrees = 184, LengthFeet = 11483 },
            new() { Identifier = "36R", HeadingDegrees = 4, LengthFeet = 11483 },
            new() { Identifier = "18R", HeadingDegrees = 184, LengthFeet = 13615 },
            new() { Identifier = "36L", HeadingDegrees = 4, LengthFeet = 13615 }
        ],
        ["LOWI"] = [
            new() { Identifier = "08", HeadingDegrees = 79, LengthFeet = 6562 },
            new() { Identifier = "26", HeadingDegrees = 259, LengthFeet = 6562 }
        ],
        ["LPMA"] = [
            new() { Identifier = "05", HeadingDegrees = 49, LengthFeet = 9124 },
            new() { Identifier = "23", HeadingDegrees = 229, LengthFeet = 9124 }
        ],

        // Middle East & Asia & Pacific Hubs
        ["OMDB"] = [
            new() { Identifier = "12L", HeadingDegrees = 120, LengthFeet = 14599 },
            new() { Identifier = "30R", HeadingDegrees = 300, LengthFeet = 14599 },
            new() { Identifier = "12R", HeadingDegrees = 120, LengthFeet = 14599 },
            new() { Identifier = "30L", HeadingDegrees = 300, LengthFeet = 14599 }
        ],
        ["OTHH"] = [
            new() { Identifier = "16L", HeadingDegrees = 160, LengthFeet = 15912 },
            new() { Identifier = "34R", HeadingDegrees = 340, LengthFeet = 15912 },
            new() { Identifier = "16R", HeadingDegrees = 160, LengthFeet = 13944 },
            new() { Identifier = "34L", HeadingDegrees = 340, LengthFeet = 13944 }
        ],
        ["VHHH"] = [
            new() { Identifier = "07L", HeadingDegrees = 74, LengthFeet = 12467 },
            new() { Identifier = "25R", HeadingDegrees = 254, LengthFeet = 12467 },
            new() { Identifier = "07C", HeadingDegrees = 74, LengthFeet = 12467 },
            new() { Identifier = "25C", HeadingDegrees = 254, LengthFeet = 12467 },
            new() { Identifier = "07R", HeadingDegrees = 74, LengthFeet = 12467 },
            new() { Identifier = "25L", HeadingDegrees = 254, LengthFeet = 12467 }
        ],
        ["RJTT"] = [
            new() { Identifier = "04", HeadingDegrees = 40, LengthFeet = 8202 },
            new() { Identifier = "22", HeadingDegrees = 220, LengthFeet = 8202 },
            new() { Identifier = "05", HeadingDegrees = 50, LengthFeet = 8858 },
            new() { Identifier = "23", HeadingDegrees = 230, LengthFeet = 8858 },
            new() { Identifier = "16L", HeadingDegrees = 157, LengthFeet = 11024 },
            new() { Identifier = "34R", HeadingDegrees = 337, LengthFeet = 11024 },
            new() { Identifier = "16R", HeadingDegrees = 157, LengthFeet = 9843 },
            new() { Identifier = "34L", HeadingDegrees = 337, LengthFeet = 9843 }
        ],
        ["WSSS"] = [
            new() { Identifier = "02L", HeadingDegrees = 24, LengthFeet = 13123 },
            new() { Identifier = "20R", HeadingDegrees = 204, LengthFeet = 13123 },
            new() { Identifier = "02C", HeadingDegrees = 24, LengthFeet = 13123 },
            new() { Identifier = "20C", HeadingDegrees = 204, LengthFeet = 13123 },
            new() { Identifier = "02R", HeadingDegrees = 24, LengthFeet = 13123 },
            new() { Identifier = "20L", HeadingDegrees = 204, LengthFeet = 13123 }
        ],
        ["YSSY"] = [
            new() { Identifier = "07", HeadingDegrees = 69, LengthFeet = 8297 },
            new() { Identifier = "25", HeadingDegrees = 249, LengthFeet = 8297 },
            new() { Identifier = "16L", HeadingDegrees = 168, LengthFeet = 7999 },
            new() { Identifier = "34R", HeadingDegrees = 348, LengthFeet = 7999 },
            new() { Identifier = "16R", HeadingDegrees = 168, LengthFeet = 12999 },
            new() { Identifier = "34L", HeadingDegrees = 348, LengthFeet = 12999 }
        ],
        ["HECA"] = [
            new() { Identifier = "05C", HeadingDegrees = 52, LengthFeet = 13120 },
            new() { Identifier = "23C", HeadingDegrees = 232, LengthFeet = 13120 },
            new() { Identifier = "05L", HeadingDegrees = 52, LengthFeet = 10827 },
            new() { Identifier = "23R", HeadingDegrees = 232, LengthFeet = 10827 },
            new() { Identifier = "05R", HeadingDegrees = 52, LengthFeet = 13120 },
            new() { Identifier = "23L", HeadingDegrees = 232, LengthFeet = 13120 }
        ]
    };

    public static double ExtractHeadingFromIdentifier(string rwyId)
    {
        if (string.IsNullOrWhiteSpace(rwyId)) return 0;
        var match = Regex.Match(rwyId.Trim(), @"^(\d{1,2})", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
        {
            var deg = (num * 10) % 360;
            return deg == 0 ? 360 : deg;
        }
        return 0;
    }

    public static string GetReciprocalIdentifier(string rwyId)
    {
        if (string.IsNullOrWhiteSpace(rwyId)) return string.Empty;
        var trimmed = rwyId.Trim().ToUpperInvariant();
        var match = Regex.Match(trimmed, @"^(\d{1,2})([LCR])?$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var num))
            return string.Empty;

        var recipNum = (num + 18) % 36;
        if (recipNum == 0) recipNum = 36;
        var prefix = recipNum.ToString("D2");

        var suffix = match.Groups[2].Success ? match.Groups[2].Value switch
        {
            "L" => "R",
            "R" => "L",
            "C" => "C",
            _ => ""
        } : "";

        return $"{prefix}{suffix}";
    }

    public static List<RunwayInfo> GetRunwaysForAirport(string icao, string? plannedRunway = null)
    {
        var cleanIcao = icao?.Trim().ToUpperInvariant() ?? string.Empty;
        if (AirportRunwayCatalog.TryGetValue(cleanIcao, out var catalogList) && catalogList.Count > 0)
        {
            return catalogList;
        }

        var result = new List<RunwayInfo>();
        if (!string.IsNullOrWhiteSpace(plannedRunway))
        {
            var h = plannedRunway.Trim().ToUpperInvariant();
            var heading = ExtractHeadingFromIdentifier(h);
            result.Add(new RunwayInfo { Identifier = h, HeadingDegrees = heading, LengthFeet = 9000 });
            var recip = GetReciprocalIdentifier(h);
            if (!string.IsNullOrEmpty(recip))
            {
                result.Add(new RunwayInfo
                {
                    Identifier = recip,
                    HeadingDegrees = (heading + 180) % 360 == 0 ? 360 : (heading + 180) % 360,
                    LengthFeet = 9000
                });
            }
        }

        if (result.Count == 0)
        {
            // Standard generic 4-way cross runway layout when airport is unknown
            result.Add(new RunwayInfo { Identifier = "09", HeadingDegrees = 90, LengthFeet = 8000 });
            result.Add(new RunwayInfo { Identifier = "27", HeadingDegrees = 270, LengthFeet = 8000 });
            result.Add(new RunwayInfo { Identifier = "18", HeadingDegrees = 180, LengthFeet = 8000 });
            result.Add(new RunwayInfo { Identifier = "36", HeadingDegrees = 360, LengthFeet = 8000 });
        }

        return result;
    }

    public static RunwayAnalysis CalculateRunway(
        string rwyId,
        double rwyHeading,
        double windDir,
        double windSpeed,
        double? windGust = null,
        double maxCrosswindLimit = 25.0,
        double maxTailwindLimit = 10.0)
    {
        var heading = rwyHeading > 0 ? rwyHeading : ExtractHeadingFromIdentifier(rwyId);
        var normWind = ((windDir % 360) + 360) % 360;
        var normHdg = ((heading % 360) + 360) % 360;

        var delta = normWind - normHdg;
        while (delta > 180) delta -= 360;
        while (delta < -180) delta += 360;

        var rad = delta * Math.PI / 180.0;
        var spd = Math.Max(0, windSpeed);
        var gst = Math.Max(spd, windGust ?? spd);

        var headwind = Math.Round(spd * Math.Cos(rad), 1);
        var crosswind = Math.Round(Math.Abs(spd * Math.Sin(rad)), 1);

        var gustHeadwind = Math.Round(gst * Math.Cos(rad), 1);
        var gustCrosswind = Math.Round(Math.Abs(gst * Math.Sin(rad)), 1);

        string side = "Calm";
        if (spd >= 2.0)
        {
            var sinVal = Math.Sin(rad);
            if (sinVal > 0.05) side = "Right";
            else if (sinVal < -0.05) side = "Left";
            else side = "Direct";
        }

        var effectiveTailwind = headwind < 0 ? -headwind : 0.0;
        var effectiveCrosswind = Math.Max(crosswind, gustCrosswind * 0.85);

        RunwaySuitability suitability;
        string suitabilityText;

        if (effectiveTailwind > maxTailwindLimit)
        {
            suitability = RunwaySuitability.LimitExceeded;
            suitabilityText = $"Tailwind {effectiveTailwind:F0} kt exceeds limit ({maxTailwindLimit:F0} kt)";
        }
        else if (effectiveCrosswind > maxCrosswindLimit)
        {
            suitability = RunwaySuitability.LimitExceeded;
            suitabilityText = $"Crosswind {effectiveCrosswind:F0} kt exceeds limit ({maxCrosswindLimit:F0} kt)";
        }
        else if (effectiveTailwind > 5.0)
        {
            suitability = RunwaySuitability.Caution;
            suitabilityText = $"Tailwind {effectiveTailwind:F0} kt caution";
        }
        else if (effectiveCrosswind > 15.0)
        {
            suitability = RunwaySuitability.Caution;
            suitabilityText = $"Crosswind {effectiveCrosswind:F0} kt caution";
        }
        else if (headwind >= 0)
        {
            suitability = RunwaySuitability.Favorable;
            suitabilityText = "Favorable";
        }
        else
        {
            suitability = RunwaySuitability.Caution;
            suitabilityText = "Light Tailwind";
        }

        var summary = headwind >= 0
            ? $"{headwind:F0} kt Headwind, {crosswind:F0} kt {side} Crosswind"
            : $"{-headwind:F0} kt Tailwind, {crosswind:F0} kt {side} Crosswind";

        if (gst > spd && (gustCrosswind > crosswind + 1 || Math.Abs(gustHeadwind) > Math.Abs(headwind) + 1))
        {
            summary += $" (G{gst:F0} kt)";
        }

        return new RunwayAnalysis
        {
            Runway = rwyId.Trim().ToUpperInvariant(),
            HeadingDegrees = heading,
            WindAngleDeltaDegrees = Math.Round(delta, 1),
            HeadwindKnots = headwind,
            CrosswindKnots = crosswind,
            CrosswindSide = side,
            GustHeadwindKnots = gustHeadwind,
            GustCrosswindKnots = gustCrosswind,
            Suitability = suitability,
            SuitabilityText = suitabilityText,
            Summary = summary
        };
    }

    public static RunwayWindReport AnalyzeStationRunways(
        string stationIcao,
        double windDir,
        double windSpeed,
        double? windGust = null,
        string? plannedRunway = null,
        double maxCrosswindLimit = 25.0,
        double maxTailwindLimit = 10.0)
    {
        var runways = GetRunwaysForAirport(stationIcao, plannedRunway);
        var analyses = new List<RunwayAnalysis>(runways.Count);

        foreach (var rwy in runways)
        {
            analyses.Add(CalculateRunway(
                rwy.Identifier,
                rwy.HeadingDegrees,
                windDir,
                windSpeed,
                windGust,
                maxCrosswindLimit,
                maxTailwindLimit));
        }

        // Score runways: prefer strong headwind, penalize crosswind, severely penalize tailwind
        // Score = Headwind * 2.0 - Crosswind * 0.8 - (Tailwind > 0 ? Tailwind * 5.0 : 0)
        var scored = analyses
            .Select(a =>
            {
                var tw = a.HeadwindKnots < 0 ? -a.HeadwindKnots : 0.0;
                var score = (a.HeadwindKnots * 2.0) - (a.CrosswindKnots * 0.8) - (tw * 5.0);
                if (a.Suitability == RunwaySuitability.LimitExceeded) score -= 100.0;
                return new { Analysis = a, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var best = scored.FirstOrDefault()?.Analysis;
        if (best != null && best.Suitability != RunwaySuitability.LimitExceeded)
        {
            best.Suitability = RunwaySuitability.Recommended;
            best.SuitabilityText = "Recommended";
        }

        string advisory;
        if (best != null)
        {
            advisory = $"Recommended RWY {best.Runway}: {best.Summary}";
            var highXw = analyses.Where(a => a.Suitability == RunwaySuitability.Caution || a.Suitability == RunwaySuitability.LimitExceeded).ToList();
            if (highXw.Count > 0)
            {
                advisory += $" • Caution: {string.Join(", ", highXw.Select(x => $"{x.Runway} ({x.SuitabilityText})"))}";
            }
        }
        else
        {
            advisory = "No suitable runway configuration identified for current winds.";
        }

        return new RunwayWindReport
        {
            StationId = stationIcao?.Trim().ToUpperInvariant() ?? "UNKN",
            WindDirectionDegrees = windDir,
            WindSpeedKnots = windSpeed,
            WindGustKnots = windGust,
            RecommendedRunway = best?.Runway,
            BestRunway = best,
            Runways = analyses,
            AdvisoryMessage = advisory
        };
    }
}
