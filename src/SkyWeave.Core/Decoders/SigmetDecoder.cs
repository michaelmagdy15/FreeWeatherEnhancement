using System.Text.RegularExpressions;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Decoders;

public partial class SigmetDecoder
{
    public List<WeatherHazard> DecodeSigmets(string rawText)
    {
        var hazards = new List<WeatherHazard>();

        var segments = rawText.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.Contains("SIGMET") || trimmed.Contains("WS") || trimmed.Contains("WST") ||
                trimmed.Contains("CONVECTIVE") || trimmed.Contains("TURB") || trimmed.Contains("ICE"))
            {
                var hazard = DecodeSingleSigmet(trimmed);
                if (hazard != null)
                    hazards.Add(hazard);
            }
        }

        return hazards;
    }

    public WeatherHazard? DecodeSingleSigmet(string sigmetText)
    {
        var hazard = new WeatherHazard
        {
            RawText = sigmetText
        };

        if (sigmetText.Contains("CONVECTIVE") || sigmetText.Contains("WST") || sigmetText.Contains("TS"))
            hazard.Type = HazardType.ConvectiveSigmet;
        else if (sigmetText.Contains("TURB") || sigmetText.Contains("LLWS"))
            hazard.Type = HazardType.TurbulenceSigmet;
        else if (sigmetText.Contains("ICE") || sigmetText.Contains("ICING"))
            hazard.Type = HazardType.IcingSigmet;
        else if (sigmetText.Contains("VOLCANIC") || sigmetText.Contains("ASH"))
            hazard.Type = HazardType.VolcanicAshSigmet;
        else if (sigmetText.Contains("AIRMET"))
            hazard.Type = HazardType.Airmet;
        else
            hazard.Type = HazardType.Sigmet;

        var idMatch = SigmetIdRegex().Match(sigmetText);
        if (idMatch.Success)
            hazard.Id = idMatch.Groups[0].Value;

        var fromToMatch = ValidTimeRegex().Match(sigmetText);
        if (fromToMatch.Success)
        {
            hazard.ValidFrom = ParseTime(fromToMatch.Groups[1].Value);
            hazard.ValidTo = ParseTime(fromToMatch.Groups[2].Value);
        }
        else
        {
            hazard.ValidFrom = DateTime.UtcNow;
            hazard.ValidTo = DateTime.UtcNow.AddHours(4);
        }

        var altMatch = AltitudeRegex().Match(sigmetText);
        if (altMatch.Success)
        {
            hazard.AltitudeMinFeet = double.Parse(altMatch.Groups[1].Value) * 100;
            hazard.AltitudeMaxFeet = double.Parse(altMatch.Groups[2].Value) * 100;
        }

        var areaMatch = AreaRegex().Match(sigmetText);
        if (areaMatch.Success)
        {
            hazard.Description = areaMatch.Groups[0].Value;
        }

        hazard.Severity = DetermineSeverity(sigmetText);

        return hazard;
    }

    private double DetermineSeverity(string text)
    {
        var upper = text.ToUpper();
        if (upper.Contains("EXTREME") || upper.Contains("SEVERE") || upper.Contains("SVR"))
            return 1.0;
        if (upper.Contains("MODERATE") || upper.Contains("MOD"))
            return 0.6;
        if (upper.Contains("LIGHT") || upper.Contains("LGT"))
            return 0.3;
        return 0.5;
    }

    private DateTime ParseTime(string timeStr)
    {
        timeStr = timeStr.TrimEnd('Z');
        if (timeStr.Length == 6 &&
            int.TryParse(timeStr[..2], out var day) &&
            int.TryParse(timeStr[2..4], out var hour) &&
            int.TryParse(timeStr[4..6], out var min))
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, day, hour, min, 0, DateTimeKind.Utc);
        }
        return DateTime.UtcNow;
    }

    [GeneratedRegex(@"\b(SIGMET|WS|WST|CONVECTIVE)\s*\d+\s*")]
    private static partial Regex SigmetIdRegex();

    [GeneratedRegex(@"(\d{6})\s*Z?\s*/?\s*(\d{6})\s*Z?")]
    private static partial Regex ValidTimeRegex();

    [GeneratedRegex(@"(?:FL|/S)\s*(\d{2,3})\s*(?:TO|THRU|-)\s*(\d{2,3})")]
    private static partial Regex AltitudeRegex();

    [GeneratedRegex(@"(?:AREA|LINE|MOV)\s+[NSEW\d\s]+")]
    private static partial Regex AreaRegex();
}
