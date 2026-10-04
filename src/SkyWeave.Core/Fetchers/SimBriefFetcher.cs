using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Fetchers;

public class SimBriefFetcher : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly Action<string>? _logger;
    private const string BaseUrl = "https://www.simbrief.com/api/xml.fetcher.php";

    public string? LastError { get; private set; }

    public SimBriefFetcher(HttpClient? httpClient = null, Action<string>? logger = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SkyWeave/1.0");
            _disposeClient = true;
        }
        _logger = logger;
    }

    public Task<SimBriefPlan?> FetchPlanAsync(string pilotIdOrUsername, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pilotIdOrUsername))
        {
            Log("Pilot ID or username is null or whitespace.");
            return Task.FromResult<SimBriefPlan?>(null);
        }

        return FetchRetry.WithRetryAsync(() => FetchInternalAsync(pilotIdOrUsername, ct), maxRetries: 3);
    }

    private async Task<SimBriefPlan?> FetchInternalAsync(string pilotIdOrUsername, CancellationToken ct)
    {
        try
        {
            var escaped = Uri.EscapeDataString(pilotIdOrUsername.Trim());
            var url = $"{BaseUrl}?username={escaped}&json=1";

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(20));

            var response = await _httpClient.GetAsync(url, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                Log($"SimBrief API returned HTTP status {(int)response.StatusCode}: {response.ReasonPhrase}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(linkedCts.Token);
            if (string.IsNullOrWhiteSpace(json))
            {
                Log("SimBrief API returned empty body.");
                return null;
            }

            var plan = DecodePlanJson(json);
            if (string.IsNullOrWhiteSpace(plan.Origin) && string.IsNullOrWhiteSpace(plan.Destination) && plan.Waypoints.Count == 0)
            {
                Log("Decoded plan contained no route, origin, or waypoints.");
                return null;
            }

            return plan;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Log("SimBrief plan fetch was cancelled by caller.");
            return null;
        }
        catch (OperationCanceledException)
        {
            Log("SimBrief plan fetch timed out after 20s.");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Log($"SimBrief network request failed: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Log($"Unexpected error fetching SimBrief plan: {ex.Message}");
            return null;
        }
    }

    public SimBriefPlan DecodePlanJson(string json)
    {
        var plan = new SimBriefPlan();
        if (string.IsNullOrWhiteSpace(json))
        {
            Log("SimBrief JSON is null or empty.");
            return plan;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                Log("SimBrief JSON root is not an object.");
                return plan;
            }

            // Check fetch status
            if (root.TryGetProperty("fetch", out var fetchElem) && fetchElem.ValueKind == JsonValueKind.Object)
            {
                if (fetchElem.TryGetProperty("status", out var statusElem))
                {
                    var status = statusElem.GetString() ?? string.Empty;
                    if (status.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"SimBrief reported error: {status}");
                        return plan;
                    }
                }
            }

            // 1. Origin
            if (root.TryGetProperty("origin", out var originElem))
            {
                plan.Origin = ExtractAirportCode(originElem);
            }

            // 2. Destination
            if (root.TryGetProperty("destination", out var destElem))
            {
                plan.Destination = ExtractAirportCode(destElem);
            }

            // 3. Alternate
            if (root.TryGetProperty("alternate", out var altElem))
            {
                plan.Alternate = ExtractAirportCode(altElem);
            }

            // 4. General section
            if (root.TryGetProperty("general", out var genElem) && genElem.ValueKind == JsonValueKind.Object)
            {
                if (genElem.TryGetProperty("flight_number", out var fnElem))
                    plan.FlightNumber = fnElem.GetString() ?? string.Empty;

                if (genElem.TryGetProperty("icao_airline", out var airlineElem) || genElem.TryGetProperty("airline", out airlineElem))
                {
                    var airline = airlineElem.GetString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(airline) && !plan.FlightNumber.StartsWith(airline, StringComparison.OrdinalIgnoreCase))
                    {
                        plan.FlightNumber = $"{airline}{plan.FlightNumber}";
                    }
                }

                if (genElem.TryGetProperty("route", out var routeElem))
                    plan.RouteString = routeElem.GetString() ?? string.Empty;
                else if (genElem.TryGetProperty("route_string", out var routeStrElem))
                    plan.RouteString = routeStrElem.GetString() ?? string.Empty;

                if (genElem.TryGetProperty("cruise_altitude", out var crzAltElem))
                    plan.CruiseAltitudeFt = ParseAltitude(crzAltElem, isCruiseAltitude: true);
                else if (genElem.TryGetProperty("initial_altitude", out var initAltElem))
                    plan.CruiseAltitudeFt = ParseAltitude(initAltElem, isCruiseAltitude: true);

                if (genElem.TryGetProperty("total_ete", out var totalEteElem))
                    plan.EstimatedTimeEnrouteMinutes = ParseEteMinutes(totalEteElem);
                else if (genElem.TryGetProperty("air_time", out var airTimeElem))
                    plan.EstimatedTimeEnrouteMinutes = ParseEteMinutes(airTimeElem);

                if (genElem.TryGetProperty("airac", out var airacElem))
                    plan.AiracCycle = airacElem.GetString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(plan.AiracCycle) && root.TryGetProperty("params", out var pElem) && pElem.ValueKind == JsonValueKind.Object)
            {
                if (pElem.TryGetProperty("airac", out var pAirac))
                    plan.AiracCycle = pAirac.GetString() ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(plan.AiracCycle))
            {
                plan.NavigraphAirac = $"Navigraph AIRAC {plan.AiracCycle}";
            }

            // 5. Aircraft section
            if (root.TryGetProperty("aircraft", out var acElem) && acElem.ValueKind == JsonValueKind.Object)
            {
                if (acElem.TryGetProperty("icaocode", out var icaoElem))
                    plan.AircraftType = icaoElem.GetString() ?? string.Empty;
                else if (acElem.TryGetProperty("type", out var typeElem))
                    plan.AircraftType = typeElem.GetString() ?? string.Empty;
                else if (acElem.TryGetProperty("name", out var nameElem))
                    plan.AircraftType = nameElem.GetString() ?? string.Empty;
            }

            // 6. Times section
            if (root.TryGetProperty("times", out var timesElem) && timesElem.ValueKind == JsonValueKind.Object)
            {
                if (timesElem.TryGetProperty("est_time_enroute_minutes", out var eteMinElem))
                    plan.EstimatedTimeEnrouteMinutes = ParseDouble(eteMinElem);
                else if (timesElem.TryGetProperty("est_time_enroute", out var eteElem))
                    plan.EstimatedTimeEnrouteMinutes = ParseEteMinutes(eteElem);
                else if (timesElem.TryGetProperty("sched_time_enroute", out var schedElem) && plan.EstimatedTimeEnrouteMinutes <= 0)
                    plan.EstimatedTimeEnrouteMinutes = ParseEteMinutes(schedElem);
            }

            // 7. Navlog / Fixes
            if (root.TryGetProperty("navlog", out var navlogElem) && navlogElem.ValueKind == JsonValueKind.Object)
            {
                if (navlogElem.TryGetProperty("fix", out var fixElem))
                {
                    if (fixElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var f in fixElem.EnumerateArray())
                        {
                            var wp = ParseWaypoint(f);
                            if (wp != null)
                                plan.Waypoints.Add(wp);
                        }
                    }
                    else if (fixElem.ValueKind == JsonValueKind.Object)
                    {
                        var wp = ParseWaypoint(fixElem);
                        if (wp != null)
                            plan.Waypoints.Add(wp);
                    }
                }
            }

            // Fallback for cruise altitude if omitted
            if (plan.CruiseAltitudeFt <= 0 && plan.Waypoints.Count > 0)
            {
                plan.CruiseAltitudeFt = plan.Waypoints.Max(w => w.AltitudeFt);
            }

            // Bounds checks (FR-B9)
            plan.CruiseAltitudeFt = Math.Clamp(plan.CruiseAltitudeFt, 0, 60000);
            plan.EstimatedTimeEnrouteMinutes = Math.Max(0, plan.EstimatedTimeEnrouteMinutes);

            return plan;
        }
        catch (JsonException ex)
        {
            Log($"JSON parsing exception in SimBrief decoder: {ex.Message}");
            return plan;
        }
        catch (Exception ex)
        {
            Log($"Unexpected exception in SimBrief decoder: {ex.Message}");
            return plan;
        }
    }

    private static SimBriefWaypoint? ParseWaypoint(JsonElement elem)
    {
        if (elem.ValueKind != JsonValueKind.Object) return null;

        try
        {
            var wp = new SimBriefWaypoint();

            if (elem.TryGetProperty("ident", out var identElem))
                wp.Identifier = identElem.GetString() ?? string.Empty;
            else if (elem.TryGetProperty("name", out var nameElem))
                wp.Identifier = nameElem.GetString() ?? string.Empty;

            if (elem.TryGetProperty("pos_lat", out var latElem))
                wp.Latitude = ParseDouble(latElem);
            else if (elem.TryGetProperty("lat", out latElem))
                wp.Latitude = ParseDouble(latElem);

            if (elem.TryGetProperty("pos_long", out var lonElem))
                wp.Longitude = ParseDouble(lonElem);
            else if (elem.TryGetProperty("lon", out lonElem) || elem.TryGetProperty("long", out lonElem))
                wp.Longitude = ParseDouble(lonElem);

            if (elem.TryGetProperty("altitude_feet", out var altElem))
                wp.AltitudeFt = ParseAltitude(altElem);
            else if (elem.TryGetProperty("altitude", out altElem) || elem.TryGetProperty("alt", out altElem))
                wp.AltitudeFt = ParseAltitude(altElem);

            if (elem.TryGetProperty("wind_dir", out var wdirElem))
                wp.WindDirection = ParseDouble(wdirElem);
            else if (elem.TryGetProperty("wind_direction", out wdirElem))
                wp.WindDirection = ParseDouble(wdirElem);

            if (elem.TryGetProperty("wind_spd", out var wspdElem))
                wp.WindSpeedKt = ParseDouble(wspdElem);
            else if (elem.TryGetProperty("wind_speed", out wspdElem))
                wp.WindSpeedKt = ParseDouble(wspdElem);

            if (elem.TryGetProperty("oat", out var oatElem))
                wp.TemperatureC = ParseDouble(oatElem);
            else if (elem.TryGetProperty("temp", out oatElem) || elem.TryGetProperty("temperature", out oatElem))
                wp.TemperatureC = ParseDouble(oatElem);

            if (elem.TryGetProperty("stage", out var stageElem))
            {
                var stageStr = stageElem.GetString()?.Trim().ToUpperInvariant() ?? string.Empty;
                wp.Stage = stageStr switch
                {
                    "CLB" or "CLIMB" => WaypointStage.Climb,
                    "CRZ" or "CRUISE" => WaypointStage.Cruise,
                    "DSC" or "DES" or "DESCENT" => WaypointStage.Descent,
                    _ => WaypointStage.Cruise
                };
            }

            // Bounds checks (FR-B9)
            wp.Latitude = Math.Clamp(wp.Latitude, -90.0, 90.0);
            wp.Longitude = Math.Clamp(wp.Longitude, -180.0, 180.0);
            wp.AltitudeFt = Math.Clamp(wp.AltitudeFt, 0, 60000);
            wp.WindDirection = (wp.WindDirection % 360.0 + 360.0) % 360.0;
            wp.WindSpeedKt = Math.Clamp(wp.WindSpeedKt, 0, 300);
            wp.TemperatureC = Math.Clamp(wp.TemperatureC, -100, 60);

            return wp;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractAirportCode(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.String)
            return elem.GetString()?.Trim() ?? string.Empty;

        if (elem.ValueKind == JsonValueKind.Object)
        {
            if (elem.TryGetProperty("icao_code", out var icao))
                return icao.GetString()?.Trim() ?? string.Empty;
            if (elem.TryGetProperty("ident", out var ident))
                return ident.GetString()?.Trim() ?? string.Empty;
            if (elem.TryGetProperty("iata_code", out var iata))
                return iata.GetString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static double ParseAltitude(JsonElement elem, bool isCruiseAltitude = false)
    {
        if (elem.ValueKind == JsonValueKind.String)
        {
            var str = elem.GetString()?.Trim();
            if (string.IsNullOrEmpty(str)) return 0;

            if (str.StartsWith("FL", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(str[2..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var fl))
                    return fl * 100.0;
            }

            if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            {
                if (isCruiseAltitude && val > 0 && val <= 600)
                    return val * 100.0;
                return val;
            }
            return 0;
        }

        if (elem.ValueKind == JsonValueKind.Number)
        {
            var val = elem.GetDouble();
            if (isCruiseAltitude && val > 0 && val <= 600)
                return val * 100.0;
            return val;
        }

        return 0;
    }

    private static double ParseEteMinutes(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.String)
        {
            var str = elem.GetString()?.Trim();
            if (string.IsNullOrEmpty(str)) return 0;

            if (str.Contains(':'))
            {
                var parts = str.Split(':');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m))
                {
                    return h * 60.0 + m;
                }
            }

            if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            {
                return val >= 600 ? val / 60.0 : val;
            }
            return 0;
        }

        if (elem.ValueKind == JsonValueKind.Number)
        {
            var val = elem.GetDouble();
            return val >= 600 ? val / 60.0 : val;
        }

        return 0;
    }

    private static double ParseDouble(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.String)
        {
            var str = elem.GetString()?.Trim();
            if (string.IsNullOrEmpty(str)) return 0;
            return double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : 0;
        }

        if (elem.ValueKind == JsonValueKind.Number)
            return elem.GetDouble();

        return 0;
    }

    private void Log(string message)
    {
        LastError = message;
        _logger?.Invoke(message);
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
