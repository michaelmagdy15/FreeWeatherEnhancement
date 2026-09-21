namespace SkyWeave.Core.Models;

/// <summary>
/// Represents parsed controller ATIS or airport weather broadcast from an online network (e.g. VATSIM/IVAO).
/// </summary>
public class VatsimAtisInfo
{
    public string IcaoId { get; set; } = string.Empty;
    public string AtisLetter { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public double? AltimeterInHg { get; set; }
    public double? AltimeterHpa { get; set; }
    public double? WindDirection { get; set; }
    public double? WindSpeedKt { get; set; }
    public double? WindGustKt { get; set; }
    public string? RunwayInUse { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    private static readonly Dictionary<string, string> LetterToPhonetic = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = "ALPHA", ["B"] = "BRAVO", ["C"] = "CHARLIE", ["D"] = "DELTA",
        ["E"] = "ECHO", ["F"] = "FOXTROT", ["G"] = "GOLF", ["H"] = "HOTEL",
        ["I"] = "INDIA", ["J"] = "JULIET", ["K"] = "KILO", ["L"] = "LIMA",
        ["M"] = "MIKE", ["N"] = "NOVEMBER", ["O"] = "OSCAR", ["P"] = "PAPA",
        ["Q"] = "QUEBEC", ["R"] = "ROMEO", ["S"] = "SIERRA", ["T"] = "TANGO",
        ["U"] = "UNIFORM", ["V"] = "VICTOR", ["W"] = "WHISKEY", ["X"] = "XRAY",
        ["Y"] = "YANKEE", ["Z"] = "ZULU"
    };

    public string AtisPhonetic =>
        !string.IsNullOrEmpty(AtisLetter) && LetterToPhonetic.TryGetValue(AtisLetter, out var phonetic)
            ? phonetic
            : AtisLetter;
}
