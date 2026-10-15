using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public sealed record ExtremeWeatherChallenge(
    string IcaoId,
    string AirportName,
    string CategoryBadge,
    string ChallengeTitle,
    double DifficultyScore, // 0..100
    double WindSpeedKnots,
    double? WindGustKnots,
    double VisibilityMeters,
    string FlightCategory,
    string RawMetar,
    double Latitude,
    double Longitude);

public sealed record FamousHistoricalStormPreset(
    string Id,
    string Title,
    string IcaoId,
    DateTime EventUtc,
    double Latitude,
    double Longitude,
    string HeadlineCondition,
    string Description);

/// <summary>
/// Viral "Bad Weather / Extreme Approach Hunter" & Curated Famous Historical Storm Preset catalog.
/// Ranks live global airport observations by crosswind/gust intensity, CAT III fog, convective storms,
/// and winter icing so pilots and streamers can jump straight into challenging real-world approaches.
/// </summary>
public class ExtremeWeatherHunterService
{
    private static readonly IReadOnlyList<(string Icao, string Name, double Lat, double Lon)> CuratedChallengeHubs =
        new List<(string, string, double, double)>
        {
            ("EGLL", "London Heathrow", 51.4700, -0.4543),
            ("EHAM", "Amsterdam Schiphol", 52.3086, 4.7639),
            ("KJFK", "New York JFK", 40.6399, -73.7787),
            ("KORD", "Chicago O'Hare", 41.9742, -87.9073),
            ("KDEN", "Denver Intl", 39.8561, -104.6737),
            ("KSFO", "San Francisco Intl", 37.6213, -122.3790),
            ("LOWI", "Innsbruck Kranebitten", 47.2602, 11.3439),
            ("LPMA", "Madeira Funchal", 32.6979, -16.7745),
            ("BIKF", "Keflavik Iceland", 63.9850, -22.6056),
            ("ENTC", "Tromso Langnes", 69.6833, 18.9189),
            ("VHHH", "Hong Kong Intl", 22.3080, 113.9185),
            ("RJTT", "Tokyo Haneda", 35.5494, 139.7798),
            ("NZWN", "Wellington Intl", -41.3272, 174.8050),
            ("SCEL", "Santiago Arturo Merino", -33.3930, -70.7858),
            ("PAJN", "Juneau Intl Alaska", 58.3550, -134.5764),
            ("CYHZ", "Halifax Stanfield", 44.8808, -63.5086)
        };

    private static readonly IReadOnlyList<FamousHistoricalStormPreset> HistoricalStormPresets =
        new List<FamousHistoricalStormPreset>
        {
            new(
                "EUNICE_EGLL_2022",
                "Storm Eunice — EGLL 'Big Jet TV' 70kt Gusts",
                "EGLL",
                new DateTime(2022, 2, 18, 12, 0, 0, DateTimeKind.Utc),
                51.4700,
                -0.4543,
                "26042G68KT // SEVERE MECHANICAL TURB",
                "The legendary Storm Eunice gale at London Heathrow that drew 200,000+ live viewers watching extreme crab angles and go-arounds on Runway 27L/27R."),
            new(
                "IAN_KMCO_2022",
                "Hurricane Ian Landfall — Central Florida",
                "KMCO",
                new DateTime(2022, 9, 28, 20, 0, 0, DateTimeKind.Utc),
                28.4294,
                -81.3089,
                "07055G78KT // +TSRA // LIFR",
                "Category 4 eyewall rainbands sweeping across Florida with blinding precipitation, severe convective windshear, and rapid barometric pressure drop."),
            new(
                "BLIZZARD_KJFK_2022",
                "North American Bomb Cyclone — New York JFK",
                "KJFK",
                new DateTime(2022, 1, 29, 15, 0, 0, DateTimeKind.Utc),
                40.6399,
                -73.7787,
                "35034G49KT // 1/4SM +SN BLSN // VV003",
                "Nor'easter bombogenesis blizzard with 1/4 SM heavy blowing snow, 49-knot crosswind gusts, and severe airframe icing."),
            new(
                "MANGKHUT_VHHH_2018",
                "Super Typhoon Mangkhut — Hong Kong VHHH",
                "VHHH",
                new DateTime(2018, 9, 16, 6, 0, 0, DateTimeKind.Utc),
                22.3080,
                113.9185,
                "09062G88KT // 0800 +SHRA // Q0972",
                "Sustained hurricane-force winds and 88-knot gusts lashing Chek Lap Kok island with extreme Lantau terrain rotor turbulence."),
            new(
                "FOEHN_LOWI_2023",
                "Alpine South Foehn Rotor — Innsbruck LOWI",
                "LOWI",
                new DateTime(2023, 11, 4, 14, 0, 0, DateTimeKind.Utc),
                47.2602,
                11.3439,
                "18032G52KT // SEVERE MOUNTAIN WAVE",
                "Classic Alpine South Foehn storm pouring over the Brenner Pass into the Inn Valley — demanding circling visual approach with lee rotors."),
            new(
                "MADEIRA_LPMA_2023",
                "Funchal Runway 05 Windshear — Madeira LPMA",
                "LPMA",
                new DateTime(2023, 10, 22, 15, 0, 0, DateTimeKind.Utc),
                32.6979,
                -16.7745,
                "02028G44KT // +SHRA // ROTOR SHEAR",
                "Iconic curved lead-in-light approach to Runway 05 with cliff-induced downdrafts and gusting Atlantic squalls.")
        };

    public IReadOnlyList<FamousHistoricalStormPreset> GetFamousHistoricalStorms() => HistoricalStormPresets;

    public IReadOnlyList<ExtremeWeatherChallenge> RankChallenges(IEnumerable<MetarData> metars)
    {
        if (metars == null) return Array.Empty<ExtremeWeatherChallenge>();

        var scored = new List<ExtremeWeatherChallenge>();
        foreach (var metar in metars)
        {
            if (metar == null || string.IsNullOrWhiteSpace(metar.StationId))
                continue;

            var challenge = ScoreMetar(metar);
            if (challenge.DifficultyScore >= 10.0)
            {
                scored.Add(challenge);
            }
        }

        return scored
            .OrderByDescending(c => c.DifficultyScore)
            .ThenByDescending(c => c.WindGustKnots ?? c.WindSpeedKnots)
            .ToList();
    }

    public ExtremeWeatherChallenge ScoreMetar(MetarData metar)
    {
        var hub = CuratedChallengeHubs.FirstOrDefault(h =>
            string.Equals(h.Icao, metar.StationId, StringComparison.OrdinalIgnoreCase));

        var airportName = !string.IsNullOrWhiteSpace(hub.Name) ? hub.Name : metar.StationId;
        var lat = hub.Lat;
        var lon = hub.Lon;

        var effectiveWind = Math.Max(metar.WindSpeedKnots, metar.WindGustKnots ?? 0);
        var gustSpread = (metar.WindGustKnots ?? metar.WindSpeedKnots) - metar.WindSpeedKnots;
        var rawUpper = (metar.RawText ?? string.Empty).ToUpperInvariant();

        bool hasThunderstorm = rawUpper.Contains("TS") || metar.Clouds.Any(c => c.Type is "CB" or "TCU");
        bool hasFreezingOrSnow = rawUpper.Contains("FZRA") || rawUpper.Contains("FZDZ") || rawUpper.Contains("+SN") || rawUpper.Contains("BLSN") || rawUpper.Contains("SN");
        bool isLowVis = metar.VisibilityMeters <= 1200;

        double windScore = Math.Clamp((effectiveWind - 12.0) * 1.8 + (gustSpread * 1.4), 0, 55);
        double visScore = metar.VisibilityMeters switch
        {
            <= 350 => 45,
            <= 800 => 36,
            <= 1600 => 26,
            <= 4800 => 14,
            _ => 0
        };
        double convectiveScore = hasThunderstorm ? 35 : 0;
        double winterScore = hasFreezingOrSnow ? 28 : 0;

        var totalScore = Math.Clamp(Math.Round(windScore + visScore + convectiveScore + winterScore, 0), 0, 100);

        string badge;
        string title;
        if (hasThunderstorm)
        {
            badge = "CONVECTIVE CB";
            title = $"{metar.StationId} Thunderstorm & Shear ({effectiveWind:F0} kt)";
        }
        else if (effectiveWind >= 28)
        {
            badge = "GALE / CROSSWIND";
            title = $"{metar.StationId} High-Gust Approach ({metar.WindSpeedKnots:F0}G{effectiveWind:F0} kt)";
        }
        else if (isLowVis)
        {
            badge = "CAT III FOG";
            title = $"{metar.StationId} Low-Visibility Mins ({metar.VisibilityMeters:F0} m)";
        }
        else if (hasFreezingOrSnow)
        {
            badge = "WINTER / ICING";
            title = $"{metar.StationId} Winter Precip & Icing ({metar.TemperatureCelsius:F0}°C)";
        }
        else
        {
            badge = metar.FlightCategory;
            title = $"{metar.StationId} Live Approach ({effectiveWind:F0} kt, {metar.VisibilityMeters:F0} m)";
        }

        return new ExtremeWeatherChallenge(
            IcaoId: metar.StationId.ToUpperInvariant(),
            AirportName: airportName,
            CategoryBadge: badge,
            ChallengeTitle: title,
            DifficultyScore: totalScore,
            WindSpeedKnots: metar.WindSpeedKnots,
            WindGustKnots: metar.WindGustKnots,
            VisibilityMeters: metar.VisibilityMeters,
            FlightCategory: string.IsNullOrWhiteSpace(metar.FlightCategory) ? "VFR" : metar.FlightCategory,
            RawMetar: metar.RawText ?? string.Empty,
            Latitude: lat,
            Longitude: lon);
    }

    public async Task<IReadOnlyList<ExtremeWeatherChallenge>> ScanLiveChallengesAsync(
        MetarFetcher metarFetcher,
        WeatherCache? cache = null,
        int maxStationsToScan = 10)
    {
        var hubs = CuratedChallengeHubs.Take(Math.Clamp(maxStationsToScan, 1, CuratedChallengeHubs.Count)).ToList();
        var tasks = hubs.Select(async hub =>
        {
            try
            {
                if (cache != null)
                {
                    return await cache.GetOrFetchAsync(
                        $"challenge:metar:{hub.Icao}",
                        () => metarFetcher.FetchMetarAsync(hub.Icao),
                        TimeSpan.FromMinutes(10));
                }
                return await metarFetcher.FetchMetarAsync(hub.Icao);
            }
            catch
            {
                return null;
            }
        });

        var results = await Task.WhenAll(tasks);
        return RankChallenges(results.Where(m => m != null)!);
    }
}
