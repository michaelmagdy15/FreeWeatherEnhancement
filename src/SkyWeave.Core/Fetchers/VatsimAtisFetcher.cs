using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.Core.Fetchers;

/// <summary>
/// Fetches and parses live VATSIM controller ATIS and airport weather broadcasts.
/// Supports both data.vatsim.net ATIS feeds and metar.vatsim.net fallbacks.
/// </summary>
public class VatsimAtisFetcher
{
    private readonly HttpClient _httpClient;
    private readonly WeatherCache _cache;

    public string VatsimDataUrl { get; set; } = "https://data.vatsim.net/v3/vatsim-data.json";
    public string VatsimMetarUrlTemplate { get; set; } = "https://metar.vatsim.net/metar.php?id={0}";

    private const string VatsimDataCacheKey = "vatsim_data_json_atis_map";

    private static readonly Dictionary<string, string> PhoneticAlphabet = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALPHA"] = "A", ["BRAVO"] = "B", ["CHARLIE"] = "C", ["DELTA"] = "D",
        ["ECHO"] = "E", ["FOXTROT"] = "F", ["GOLF"] = "G", ["HOTEL"] = "H",
        ["INDIA"] = "I", ["JULIET"] = "J", ["JULIETT"] = "J", ["KILO"] = "K",
        ["LIMA"] = "L", ["MIKE"] = "M", ["NOVEMBER"] = "N", ["OSCAR"] = "O",
        ["PAPA"] = "P", ["QUEBEC"] = "Q", ["ROMEO"] = "R", ["SIERRA"] = "S",
        ["TANGO"] = "T", ["UNIFORM"] = "U", ["VICTOR"] = "V", ["WHISKEY"] = "W",
        ["XRAY"] = "X", ["X-RAY"] = "X", ["YANKEE"] = "Y", ["ZULU"] = "Z"
    };

    public VatsimAtisFetcher(HttpClient? httpClient = null, WeatherCache? cache = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _cache = cache ?? new WeatherCache();
    }

    /// <summary>
    /// Fetches live VATSIM ATIS or airport weather for the given ICAO code.
    /// Checks controller ATIS first, falling back to VATSIM METAR if offline.
    /// </summary>
    public Task<VatsimAtisInfo?> GetAtisAsync(string icao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(icao))
            return Task.FromResult<VatsimAtisInfo?>(null);

        var cleanIcao = icao.Trim().ToUpperInvariant();
        var cacheKey = $"vatsim_atis_{cleanIcao}";

        return _cache.GetOrFetchAsync(cacheKey, () => FetchRetry.WithRetryAsync(() => FetchInternalAsync(cleanIcao, ct), maxRetries: 3), TimeSpan.FromSeconds(30));
    }

    private async Task<VatsimAtisInfo?> FetchInternalAsync(string cleanIcao, CancellationToken ct)
    {
        // 1. Try to obtain controller ATIS from vatsim-data.json
        var atisMap = await GetOrFetchVatsimDataAtisMapAsync(ct);
        if (atisMap != null && atisMap.TryGetValue(cleanIcao, out var rawEntry))
        {
            var parsed = ParseAtis(rawEntry.Text, cleanIcao);
            if (parsed != null)
            {
                if (string.IsNullOrEmpty(parsed.AtisLetter) && !string.IsNullOrEmpty(rawEntry.AtisCode))
                {
                    parsed.AtisLetter = rawEntry.AtisCode.ToUpperInvariant();
                }

                if (rawEntry.LastUpdated != default)
                {
                    parsed.Timestamp = rawEntry.LastUpdated;
                }

                return parsed;
            }
        }

        // 2. Fallback to metar.vatsim.net
        return await FetchVatsimMetarAsync(cleanIcao, ct);
    }

    private async Task<Dictionary<string, (string Callsign, string AtisCode, string Text, DateTime LastUpdated)>?> GetOrFetchVatsimDataAtisMapAsync(CancellationToken ct)
    {
        if (_cache.TryGet<Dictionary<string, (string, string, string, DateTime)>>(VatsimDataCacheKey, out var cachedMap) && cachedMap != null)
        {
            return cachedMap;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var response = await _httpClient.GetAsync(VatsimDataUrl, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

            if (!doc.RootElement.TryGetProperty("atis", out var atisArr) || atisArr.ValueKind != JsonValueKind.Array)
                return null;

            var map = new Dictionary<string, (string Callsign, string AtisCode, string Text, DateTime LastUpdated)>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in atisArr.EnumerateArray())
            {
                var callsign = entry.TryGetProperty("callsign", out var csProp) ? csProp.GetString() ?? "" : "";
                var atisCode = entry.TryGetProperty("atis_code", out var codeProp) ? codeProp.GetString() ?? "" : "";

                string text = "";
                if (entry.TryGetProperty("text_atis", out var textProp))
                {
                    if (textProp.ValueKind == JsonValueKind.Array)
                    {
                        var lines = new List<string>();
                        foreach (var l in textProp.EnumerateArray())
                        {
                            var s = l.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                                lines.Add(s.Trim());
                        }
                        text = string.Join(" ", lines);
                    }
                    else if (textProp.ValueKind == JsonValueKind.String)
                    {
                        text = textProp.GetString() ?? "";
                    }
                }

                DateTime lastUpdated = DateTime.UtcNow;
                if (entry.TryGetProperty("last_updated", out var luProp) &&
                    DateTime.TryParse(luProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedLu))
                {
                    lastUpdated = parsedLu;
                }

                var icao = ExtractIcaoFromCallsign(callsign);
                if (!string.IsNullOrEmpty(icao) && !string.IsNullOrWhiteSpace(text))
                {
                    // If multiple (e.g. separate ARR/DEP ATIS), store first or combined
                    if (!map.ContainsKey(icao))
                    {
                        map[icao] = (callsign, atisCode, text, lastUpdated);
                    }
                }
            }

            _cache.Set(VatsimDataCacheKey, map, TimeSpan.FromSeconds(30));
            return map;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<VatsimAtisInfo?> FetchVatsimMetarAsync(string cleanIcao, CancellationToken ct)
    {
        try
        {
            var url = string.Format(VatsimMetarUrlTemplate, cleanIcao);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = (await response.Content.ReadAsStringAsync(cts.Token)).Trim();
            if (string.IsNullOrWhiteSpace(body) || body.Contains("No METAR", StringComparison.OrdinalIgnoreCase))
                return null;

            return ParseAtis(body, cleanIcao);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static string ExtractIcaoFromCallsign(string callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign))
            return string.Empty;

        var parts = callsign.Split('_');
        if (parts.Length > 0 && parts[0].Length is 3 or 4)
        {
            return parts[0].ToUpperInvariant();
        }

        if (callsign.EndsWith("ATIS", StringComparison.OrdinalIgnoreCase) && callsign.Length is 7 or 8)
        {
            return callsign[..^4].ToUpperInvariant();
        }

        return string.Empty;
    }

    /// <summary>
    /// Parses raw ATIS or METAR text into a structured VatsimAtisInfo model.
    /// Extracts controller-broadcast QNH / Altimeter, ATIS letter, wind, and runway in use.
    /// </summary>
    public VatsimAtisInfo? ParseAtis(string rawAtisText, string icao)
    {
        if (string.IsNullOrWhiteSpace(rawAtisText))
            return null;

        var cleanIcao = !string.IsNullOrWhiteSpace(icao)
            ? icao.Trim().ToUpperInvariant()
            : ExtractIcao(rawAtisText);

        var letter = ExtractAtisLetter(rawAtisText);
        var (inHg, hPa) = ExtractAltimeter(rawAtisText);
        var (windDir, windSpd, windGst) = ExtractWind(rawAtisText);
        var runway = ExtractRunway(rawAtisText);
        var timestamp = ExtractTimestamp(rawAtisText);

        // If no recognizable weather or ATIS fields are present, return null
        if (string.IsNullOrEmpty(letter) &&
            inHg == null &&
            hPa == null &&
            windDir == null &&
            windSpd == null &&
            string.IsNullOrEmpty(runway))
        {
            return null;
        }

        return new VatsimAtisInfo
        {
            IcaoId = cleanIcao,
            AtisLetter = letter,
            RawText = rawAtisText.Trim(),
            AltimeterInHg = inHg,
            AltimeterHpa = hPa,
            WindDirection = windDir,
            WindSpeedKt = windSpd,
            WindGustKt = windGst,
            RunwayInUse = runway,
            Timestamp = timestamp
        };
    }

    private static string ExtractIcao(string text)
    {
        var mAtis = Regex.Match(text, @"\b(?:ATIS\s+)?([A-Z]{4})\s+ATIS\b", RegexOptions.IgnoreCase);
        if (mAtis.Success) return mAtis.Groups[1].Value.ToUpperInvariant();

        var mStart = Regex.Match(text, @"^([A-Z]{4})\b", RegexOptions.IgnoreCase);
        if (mStart.Success) return mStart.Groups[1].Value.ToUpperInvariant();

        return string.Empty;
    }

    private static string ExtractAtisLetter(string text)
    {
        // 1. ATIS <ICAO> <LETTER> METAR (e.g. ATIS EDDC G METAR)
        var mMetar = Regex.Match(text, @"\bATIS\s+[A-Z0-9]{4}\s+(?<tok>[A-Za-z])\s+METAR\b", RegexOptions.IgnoreCase);
        if (mMetar.Success)
            return ResolveLetter(mMetar.Groups["tok"].Value);

        // 2. (ATIS )?(INFO|INFORMATION) (IS )?<LETTER or PHONETIC> (e.g. INFO BRAVO, INFORMATION JULIET, ATIS INFO W, INFO. BRAVO)
        var mInfo = Regex.Match(text, @"\b(?:ATIS\s+)?(?:INFO|INFORMATION)\.?(?:\s+IS)?\s+(?<tok>[A-Za-z]+)\b", RegexOptions.IgnoreCase);
        if (mInfo.Success)
        {
            var candidate = mInfo.Groups["tok"].Value;
            if (candidate.Length == 1 || PhoneticAlphabet.ContainsKey(candidate))
                return ResolveLetter(candidate);
        }

        // 3. (ADVS|ADVISE)...(INFO|INFORMATION) <LETTER or PHONETIC>
        var mAdv = Regex.Match(text, @"\b(?:ADVS|ADVISE)(?:[^\.]*?)(?:INFO|INFORMATION)\s+(?<tok>[A-Za-z]+)\b", RegexOptions.IgnoreCase);
        if (mAdv.Success)
        {
            var candidate = mAdv.Groups["tok"].Value;
            if (candidate.Length == 1 || PhoneticAlphabet.ContainsKey(candidate))
                return ResolveLetter(candidate);
        }

        // 4. ATIS <LETTER or PHONETIC> (e.g. ATIS B, ATIS BRAVO)
        var mAtis = Regex.Match(text, @"\bATIS\s+(?:CODE\s+)?(?<tok>[A-Za-z]+)\b", RegexOptions.IgnoreCase);
        if (mAtis.Success)
        {
            var candidate = mAtis.Groups["tok"].Value;
            if (candidate.Length == 1 || PhoneticAlphabet.ContainsKey(candidate))
                return ResolveLetter(candidate);
        }

        return string.Empty;
    }

    private static string ResolveLetter(string tok)
    {
        if (PhoneticAlphabet.TryGetValue(tok, out var single))
            return single;

        return tok.Length == 1 ? tok.ToUpperInvariant() : string.Empty;
    }

    private static (double? inHg, double? hPa) ExtractAltimeter(string text)
    {
        // 1. Explicit ALTIMETER / ALTM / ALT pattern:
        // e.g. "ALTIMETER 29.92", "ALTIMETER 2992", "ALTIMETER IS 29.92", "ALT 29.92", "ALTM 2992"
        var mAlt = Regex.Match(text, @"\b(?:ALTIMETER|ALTM|ALT)(?:\s+IS)?\s+(?<val>\d{2}[\.,]\d{2}|\d{4})\b", RegexOptions.IgnoreCase);
        if (mAlt.Success)
        {
            var valStr = mAlt.Groups["val"].Value.Replace(',', '.');
            if (valStr.Contains('.'))
            {
                if (double.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var inHg))
                {
                    var hPa = Math.Round(inHg * 33.8639, 1);
                    return (Math.Round(inHg, 2), hPa);
                }
            }
            else if (int.TryParse(valStr, out var rawInt))
            {
                var inHg = rawInt / 100.0;
                var hPa = Math.Round(inHg * 33.8639, 1);
                return (Math.Round(inHg, 2), hPa);
            }
        }

        // 2. QNH pattern: e.g. "QNH 1013", "QNH1024", "QNH IS 1026", "QNH 1013 HPA"
        var mQnh = Regex.Match(text, @"\bQNH(?:\s+IS)?\s*(?<val>\d{3,4})\b", RegexOptions.IgnoreCase);
        if (mQnh.Success && double.TryParse(mQnh.Groups["val"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var qnhHpa))
        {
            var inHg = Math.Round(qnhHpa / 33.8639, 2);
            return (inHg, qnhHpa);
        }

        // 3. METAR style Q-code: e.g. "Q1013", "Q0998"
        var mQCode = Regex.Match(text, @"(?<![A-Z0-9])Q(?<val>\d{3,4})\b", RegexOptions.IgnoreCase);
        if (mQCode.Success && double.TryParse(mQCode.Groups["val"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var qCodeHpa))
        {
            var inHg = Math.Round(qCodeHpa / 33.8639, 2);
            return (inHg, qCodeHpa);
        }

        // 4. METAR style A-code: e.g. "A2992", "A2981", "A3010"
        var mACode = Regex.Match(text, @"(?<![A-Z0-9])A(?<val>\d{4})\b", RegexOptions.IgnoreCase);
        if (mACode.Success && int.TryParse(mACode.Groups["val"].Value, out var aInt))
        {
            var inHg = Math.Round(aInt / 100.0, 2);
            var hPa = Math.Round(inHg * 33.8639, 1);
            return (inHg, hPa);
        }

        return (null, null);
    }

    private static (double? dir, double? spd, double? gst) ExtractWind(string text)
    {
        // 1. Spoken verbal wind calm: e.g. "WIND CALM", "WINDS ARE CALM", "WIND IS CALM"
        if (Regex.IsMatch(text, @"\bWINDS?\s+(?:ARE\s+|IS\s+)?CALM\b", RegexOptions.IgnoreCase))
        {
            return (0, 0, null);
        }

        // 2. Spoken verbal wind variable: e.g. "WIND VARIABLE AT 4 KNOTS", "WINDS VRB 03KT"
        var mVrb = Regex.Match(text, @"\bWINDS?\s+(?:ARE\s+|IS\s+)?(?:VARIABLE|VRB)(?:\s+AT)?\s+(?<spd>\d{1,3})(?:\s*(?:KNOTS|KTS|KT))?\b", RegexOptions.IgnoreCase);
        if (mVrb.Success && double.TryParse(mVrb.Groups["spd"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var vrbSpd))
        {
            return (0, vrbSpd, null);
        }

        // 3. Spoken verbal wind:
        // e.g. "WIND 270 AT 12 KNOTS", "WIND 270 AT 12", "WIND 330 DEGREES 5 KNOTS", "WIND 270 AT 12 GUSTS 20 KNOTS"
        var mSpoken = Regex.Match(text,
            @"\bWINDS?\s+(?:ARE\s+|IS\s+)?(?<dir>\d{3})\s*(?:DEGREES|DEG)?\s*(?:AT|\/|\s)\s*(?<spd>\d{1,3})(?:\s*(?:GUSTING|GUSTS|G)\s*(?:TO\s*)?(?<gst>\d{1,3}))?\s*(?:KNOTS|KTS|KT)?\b",
            RegexOptions.IgnoreCase);
        if (mSpoken.Success &&
            double.TryParse(mSpoken.Groups["dir"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sDir) &&
            double.TryParse(mSpoken.Groups["spd"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sSpd))
        {
            double? sGst = mSpoken.Groups["gst"].Success && double.TryParse(mSpoken.Groups["gst"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gVal)
                ? gVal
                : null;
            return (sDir, sSpd, sGst);
        }

        // 4. METAR format wind:
        // e.g. "27012KT", "27012G20KT", "08003KT", "00000KT", "VRB03KT"
        var mMetar = Regex.Match(text, @"\b(?<dir>\d{3}|VRB)(?<spd>\d{2,3})(?:G(?<gst>\d{2,3}))?KT\b", RegexOptions.IgnoreCase);
        if (mMetar.Success)
        {
            var dirStr = mMetar.Groups["dir"].Value;
            double dir = dirStr.Equals("VRB", StringComparison.OrdinalIgnoreCase) ? 0 : double.Parse(dirStr, CultureInfo.InvariantCulture);
            double spd = double.Parse(mMetar.Groups["spd"].Value, CultureInfo.InvariantCulture);
            double? gst = mMetar.Groups["gst"].Success ? double.Parse(mMetar.Groups["gst"].Value, CultureInfo.InvariantCulture) : null;
            return (dir, spd, gst);
        }

        return (null, null, null);
    }

    private static string? ExtractRunway(string text)
    {
        var mRwy = Regex.Match(text,
            @"\b(?:RUNWAYS?\s+IN\s+USE|RWYS?\s+IN\s+USE|LANDING\s+RUNWAYS?|LANDING\s+RWYS?|DEPTG\s+RWYS?|DEPG\s+RWY|EXPECT(?:\s+ILS|\s+VISUAL)?\s+APPROACH(?:\s+RUNWAY|\s+RWY)?|RUNWAY|RWY|RY)\s+(?<rwy>\d{2}[LCR]?(?:\s*(?:AND|,|\/)\s*\d{2}[LCR]?)*)\b",
            RegexOptions.IgnoreCase);

        if (mRwy.Success)
        {
            var rawRwy = mRwy.Groups["rwy"].Value.Trim();
            if (!string.IsNullOrEmpty(rawRwy))
            {
                return Regex.Replace(rawRwy, @"\s+AND\s+", ", ", RegexOptions.IgnoreCase);
            }
        }

        return null;
    }

    private static DateTime ExtractTimestamp(string text)
    {
        var now = DateTime.UtcNow;

        // 6-digit zulu timestamp: e.g. "211420Z"
        var m6z = Regex.Match(text, @"\b(?<day>\d{2})(?<hr>\d{2})(?<min>\d{2})Z\b", RegexOptions.IgnoreCase);
        if (m6z.Success &&
            int.TryParse(m6z.Groups["day"].Value, out var day) &&
            int.TryParse(m6z.Groups["hr"].Value, out var hr) &&
            int.TryParse(m6z.Groups["min"].Value, out var min) &&
            hr < 24 && min < 60)
        {
            var year = now.Year;
            var month = now.Month;
            if (day > DateTime.DaysInMonth(year, month))
                day = now.Day;

            return new DateTime(year, month, day, hr, min, 0, DateTimeKind.Utc);
        }

        // 4-digit zulu timestamp: e.g. "1353Z" or "TIME 1420"
        var m4z = Regex.Match(text, @"\b(?:TIME\s+)?(?<hr>\d{2})(?<min>\d{2})Z?\b", RegexOptions.IgnoreCase);
        if (m4z.Success &&
            int.TryParse(m4z.Groups["hr"].Value, out var hr4) &&
            int.TryParse(m4z.Groups["min"].Value, out var min4) &&
            hr4 < 24 && min4 < 60)
        {
            return new DateTime(now.Year, now.Month, now.Day, hr4, min4, 0, DateTimeKind.Utc);
        }

        return now;
    }
}
