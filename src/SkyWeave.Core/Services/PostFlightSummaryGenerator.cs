using System.Text;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public sealed record PostFlightSummaryReport(
    string DepartureStation,
    string ArrivalStation,
    int StationsTraversedCount,
    IReadOnlyList<string> StationsTraversed,
    double PeakWindKnots,
    double PeakGustKnots,
    double MinimumVisibilityMeters,
    double PeakTurbulenceIndex,
    double PeakIcingIndex,
    double PeakStructuralIcePercent,
    int WakeVortexEncounters,
    string WeatherDifficultyGrade,
    string ShareableMarkdownCard,
    DateTime GeneratedAtUtc);

/// <summary>
/// Tracks live weather telemetry across a flight session and generates a shareable
/// "Post-Flight Weather Replay Summary Card" for Discord, Reddit, and VA PIREPs.
/// </summary>
public class PostFlightSummaryGenerator
{
    private readonly object _lock = new();
    private readonly List<string> _stationsTraversed = new();
    private double _peakWindKnots;
    private double _peakGustKnots;
    private double _minVisibilityMeters = 10000;
    private bool _hasSample;
    private double _peakTurbulenceIndex;
    private double _peakIcingIndex;
    private double _peakStructuralIcePct;
    private int _wakeEncounters;

    public void RecordSnapshot(WeatherState? state, bool wakeEncounterActive = false)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.StationId))
            return;

        lock (_lock)
        {
            var station = state.StationId.Trim().ToUpperInvariant();
            if (_stationsTraversed.Count == 0 || !string.Equals(_stationsTraversed[^1], station, StringComparison.OrdinalIgnoreCase))
            {
                _stationsTraversed.Add(station);
            }

            var wind = state.WindSpeedKnots;
            var gust = state.WindGustKnots ?? state.WindSpeedKnots;
            if (wind > _peakWindKnots) _peakWindKnots = wind;
            if (gust > _peakGustKnots) _peakGustKnots = gust;

            if (!_hasSample || state.VisibilityMeters < _minVisibilityMeters)
            {
                _minVisibilityMeters = Math.Max(50, state.VisibilityMeters);
                _hasSample = true;
            }

            if (state.TurbulenceIndex > _peakTurbulenceIndex)
                _peakTurbulenceIndex = state.TurbulenceIndex;

            if (state.IcingIndex > _peakIcingIndex)
                _peakIcingIndex = state.IcingIndex;

            if (state.StructuralIcePercent.HasValue && state.StructuralIcePercent.Value > _peakStructuralIcePct)
                _peakStructuralIcePct = state.StructuralIcePercent.Value;

            if (wakeEncounterActive || state.TurbulenceLayers.Any(t => t.Type == TurbulenceType.Wake))
                _wakeEncounters++;
        }
    }

    public void ResetSession()
    {
        lock (_lock)
        {
            _stationsTraversed.Clear();
            _peakWindKnots = 0;
            _peakGustKnots = 0;
            _minVisibilityMeters = 10000;
            _hasSample = false;
            _peakTurbulenceIndex = 0;
            _peakIcingIndex = 0;
            _peakStructuralIcePct = 0;
            _wakeEncounters = 0;
        }
    }

    public PostFlightSummaryReport GenerateReport(WeatherState? currentFallback = null)
    {
        lock (_lock)
        {
            if (!_hasSample && currentFallback != null)
            {
                RecordSnapshot(currentFallback);
            }

            var dep = _stationsTraversed.Count > 0 ? _stationsTraversed[0] : "UNKN";
            var arr = _stationsTraversed.Count > 0 ? _stationsTraversed[^1] : "UNKN";
            var stations = _stationsTraversed.ToList();

            var compositeSeverity = (_peakGustKnots / 55.0 * 35.0)
                                    + (_peakTurbulenceIndex * 35.0)
                                    + (_peakIcingIndex * 20.0)
                                    + (_minVisibilityMeters < 1500 ? 20.0 : 0.0);

            var grade = compositeSeverity switch
            {
                >= 65 => "S-RANK // EXTREME STORM SURVIVOR",
                >= 45 => "A-RANK // HARD IFR & TURBULENCE",
                >= 25 => "B-RANK // CHALLENGING CROSSWIND / IMC",
                _ => "C-RANK // SMOOTH STANDARD DISPATCH"
            };

            var sb = new StringBuilder();
            sb.AppendLine($"✈️ **SkyWeave v0.8.0 — Post-Flight Weather Replay ({dep} ➔ {arr})**");
            sb.AppendLine($"🏆 **Difficulty Grade:** `{grade}`");
            sb.AppendLine($"💨 **Peak Surface Wind / Gust:** `{_peakWindKnots:F0} kt` (Gusting `{_peakGustKnots:F0} kt`)");
            sb.AppendLine($"🌫️ **Minimum Visibility:** `{_minVisibilityMeters:F0} m` (`{_minVisibilityMeters / 1609.344:F1} SM`)");
            sb.AppendLine($"⚡ **Peak Turbulence / Icing Index:** `CAT {_peakTurbulenceIndex * 100:F0}%` | `ICE {_peakIcingIndex * 100:F0}%` (Airframe Ice `{_peakStructuralIcePct:F1}%`)");
            if (_wakeEncounters > 0)
            {
                sb.AppendLine($"🌀 **Wake Vortex Encounters:** `{_wakeEncounters}`");
            }
            sb.AppendLine($"📡 **Stations Traversed ({stations.Count}):** `{string.Join(" → ", stations.Take(10))}`");
            sb.AppendLine($"_Powered by SkyWeave — Free Open-Source MSFS 2024 Weather Engine_");

            return new PostFlightSummaryReport(
                DepartureStation: dep,
                ArrivalStation: arr,
                StationsTraversedCount: stations.Count,
                StationsTraversed: stations,
                PeakWindKnots: Math.Round(_peakWindKnots, 1),
                PeakGustKnots: Math.Round(_peakGustKnots, 1),
                MinimumVisibilityMeters: Math.Round(_minVisibilityMeters, 0),
                PeakTurbulenceIndex: Math.Round(_peakTurbulenceIndex, 2),
                PeakIcingIndex: Math.Round(_peakIcingIndex, 2),
                PeakStructuralIcePercent: Math.Round(_peakStructuralIcePct, 1),
                WakeVortexEncounters: _wakeEncounters,
                WeatherDifficultyGrade: grade,
                ShareableMarkdownCard: sb.ToString().Trim(),
                GeneratedAtUtc: DateTime.UtcNow);
        }
    }
}
