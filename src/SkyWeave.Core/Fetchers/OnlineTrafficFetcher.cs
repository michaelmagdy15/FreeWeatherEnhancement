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

/// <summary>
/// Fetches and decodes live online virtual air traffic from VATSIM and IVAO networks.
/// Adheres to IVAO and VATSIM rate-limiting policies and handles schema resilience.
/// </summary>
public class OnlineTrafficFetcher : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly Action<string>? _logger;

    public string VatsimDataUrl { get; set; } = "https://data.vatsim.net/v3/vatsim-data.json";
    public string IvaoDataUrl { get; set; } = "https://api.ivao.aero/v2/tracker/whazzup";

    private string? _cachedVatsimJson;
    private DateTime _lastVatsimFetch = DateTime.MinValue;
    private readonly SemaphoreSlim _vatsimLock = new(1, 1);

    private string? _cachedIvaoJson;
    private DateTime _lastIvaoFetch = DateTime.MinValue;
    private readonly SemaphoreSlim _ivaoLock = new(1, 1);

    private static readonly TimeSpan VatsimCacheDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan IvaoCacheDuration = TimeSpan.FromSeconds(30); // Guarded per IVAO TOS (min 15s)

    public OnlineTrafficFetcher(HttpClient? httpClient = null, Action<string>? logger = null)
    {
        _logger = logger;
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SkyWeave/1.0 (+https://github.com/michaelmagdy15/FreeWeatherEnhancement)");
            _disposeClient = true;
        }
    }

    /// <summary>
    /// Fetches all active flights from enabled networks within the specified radius of the center position.
    /// </summary>
    public async Task<List<OnlineFlightData>> GetNearbyTrafficAsync(
        double centerLat,
        double centerLon,
        double maxDistanceNm = 400.0,
        bool includeVatsim = true,
        bool includeIvao = true,
        CancellationToken ct = default)
    {
        var vatsimTask = includeVatsim
            ? FetchVatsimPilotsSafeAsync(centerLat, centerLon, maxDistanceNm, ct)
            : Task.FromResult(new List<OnlineFlightData>());

        var ivaoTask = includeIvao
            ? FetchIvaoPilotsSafeAsync(centerLat, centerLon, maxDistanceNm, ct)
            : Task.FromResult(new List<OnlineFlightData>());

        await Task.WhenAll(vatsimTask, ivaoTask);

        var results = new List<OnlineFlightData>();
        results.AddRange(await vatsimTask);
        results.AddRange(await ivaoTask);

        return results
            .OrderBy(f => f.DistanceNm)
            .ToList();
    }

    public async Task<List<OnlineFlightData>> FetchVatsimPilotsSafeAsync(
        double centerLat,
        double centerLon,
        double maxDistanceNm,
        CancellationToken ct)
    {
        try
        {
            var json = await GetVatsimJsonAsync(ct);
            if (string.IsNullOrWhiteSpace(json)) return new List<OnlineFlightData>();
            return DecodeVatsimPilots(json, centerLat, centerLon, maxDistanceNm);
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"VATSIM traffic fetch warning: {ex.Message}");
            return new List<OnlineFlightData>();
        }
    }

    public async Task<List<OnlineFlightData>> FetchIvaoPilotsSafeAsync(
        double centerLat,
        double centerLon,
        double maxDistanceNm,
        CancellationToken ct)
    {
        try
        {
            var json = await GetIvaoJsonAsync(ct);
            if (string.IsNullOrWhiteSpace(json)) return new List<OnlineFlightData>();
            return DecodeIvaoPilots(json, centerLat, centerLon, maxDistanceNm);
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"IVAO traffic fetch warning: {ex.Message}");
            return new List<OnlineFlightData>();
        }
    }

    private async Task<string?> GetVatsimJsonAsync(CancellationToken ct)
    {
        await _vatsimLock.WaitAsync(ct);
        try
        {
            if (_cachedVatsimJson != null && (DateTime.UtcNow - _lastVatsimFetch) < VatsimCacheDuration)
            {
                return _cachedVatsimJson;
            }

            var response = await _httpClient.GetStringAsync(VatsimDataUrl, ct);
            _cachedVatsimJson = response;
            _lastVatsimFetch = DateTime.UtcNow;
            return response;
        }
        finally
        {
            _vatsimLock.Release();
        }
    }

    private async Task<string?> GetIvaoJsonAsync(CancellationToken ct)
    {
        await _ivaoLock.WaitAsync(ct);
        try
        {
            if (_cachedIvaoJson != null && (DateTime.UtcNow - _lastIvaoFetch) < IvaoCacheDuration)
            {
                return _cachedIvaoJson;
            }

            var response = await _httpClient.GetStringAsync(IvaoDataUrl, ct);
            _cachedIvaoJson = response;
            _lastIvaoFetch = DateTime.UtcNow;
            return response;
        }
        finally
        {
            _ivaoLock.Release();
        }
    }

    public List<OnlineFlightData> DecodeVatsimPilots(
        string json,
        double? centerLat = null,
        double? centerLon = null,
        double maxDistanceNm = 400.0)
    {
        var flights = new List<OnlineFlightData>();
        if (string.IsNullOrWhiteSpace(json)) return flights;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("pilots", out var pilotsElem) || pilotsElem.ValueKind != JsonValueKind.Array)
                return flights;

            foreach (var pilot in pilotsElem.EnumerateArray())
            {
                if (pilot.ValueKind != JsonValueKind.Object) continue;

                var lat = TryGetDouble(pilot, "latitude");
                var lon = TryGetDouble(pilot, "longitude");

                var distNm = 0.0;
                if (centerLat.HasValue && centerLon.HasValue)
                {
                    distNm = CalculateDistanceNm(centerLat.Value, centerLon.Value, lat, lon);
                    if (distNm > maxDistanceNm) continue;
                }

                var flight = new OnlineFlightData
                {
                    Network = OnlineNetwork.Vatsim,
                    Callsign = TryGetString(pilot, "callsign"),
                    PilotName = TryGetString(pilot, "name"),
                    Latitude = lat,
                    Longitude = lon,
                    AltitudeFeet = TryGetDouble(pilot, "altitude"),
                    HeadingDegrees = TryGetDouble(pilot, "heading"),
                    GroundSpeedKnots = TryGetDouble(pilot, "groundspeed"),
                    DistanceNm = distNm,
                    LastUpdated = DateTime.UtcNow
                };

                if (pilot.TryGetProperty("flight_plan", out var fp) && fp.ValueKind == JsonValueKind.Object)
                {
                    flight.AircraftType = TryGetString(fp, "aircraft_short");
                    if (string.IsNullOrEmpty(flight.AircraftType))
                        flight.AircraftType = TryGetString(fp, "aircraft");

                    flight.Departure = TryGetString(fp, "departure");
                    flight.Arrival = TryGetString(fp, "arrival");
                }

                flights.Add(flight);
            }
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"Error parsing VATSIM pilots JSON: {ex.Message}");
        }

        return flights;
    }

    public List<OnlineFlightData> DecodeIvaoPilots(
        string json,
        double? centerLat = null,
        double? centerLon = null,
        double maxDistanceNm = 400.0)
    {
        var flights = new List<OnlineFlightData>();
        if (string.IsNullOrWhiteSpace(json)) return flights;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("clients", out var clientsElem) || clientsElem.ValueKind != JsonValueKind.Object)
                return flights;

            if (!clientsElem.TryGetProperty("pilots", out var pilotsElem) || pilotsElem.ValueKind != JsonValueKind.Array)
                return flights;

            foreach (var pilot in pilotsElem.EnumerateArray())
            {
                if (pilot.ValueKind != JsonValueKind.Object) continue;

                double lat = 0, lon = 0, alt = 0, hdg = 0, gs = 0;
                bool onGround = false;

                if (pilot.TryGetProperty("lastTrack", out var track) && track.ValueKind == JsonValueKind.Object)
                {
                    lat = TryGetDouble(track, "latitude");
                    lon = TryGetDouble(track, "longitude");
                    alt = TryGetDouble(track, "altitude");
                    hdg = TryGetDouble(track, "heading");
                    gs = TryGetDouble(track, "groundSpeed");
                    if (track.TryGetProperty("onGround", out var ogElem) && ogElem.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        onGround = ogElem.GetBoolean();
                    }
                }

                var distNm = 0.0;
                if (centerLat.HasValue && centerLon.HasValue)
                {
                    distNm = CalculateDistanceNm(centerLat.Value, centerLon.Value, lat, lon);
                    if (distNm > maxDistanceNm) continue;
                }

                var flight = new OnlineFlightData
                {
                    Network = OnlineNetwork.Ivao,
                    Callsign = TryGetString(pilot, "callsign"),
                    Latitude = lat,
                    Longitude = lon,
                    AltitudeFeet = alt,
                    HeadingDegrees = hdg,
                    GroundSpeedKnots = gs,
                    IsOnGround = onGround,
                    DistanceNm = distNm,
                    LastUpdated = DateTime.UtcNow
                };

                if (pilot.TryGetProperty("flightPlan", out var fp) && fp.ValueKind == JsonValueKind.Object)
                {
                    flight.AircraftType = TryGetString(fp, "aircraftId");
                    flight.Departure = TryGetString(fp, "departureId");
                    flight.Arrival = TryGetString(fp, "arrivalId");
                }

                flights.Add(flight);
            }
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"Error parsing IVAO pilots JSON: {ex.Message}");
        }

        return flights;
    }

    public static double CalculateDistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return 6371.0 * c * 0.539957; // km to NM
    }

    private static string TryGetString(JsonElement obj, string propertyName)
    {
        if (obj.TryGetProperty(propertyName, out var elem))
        {
            return elem.ValueKind switch
            {
                JsonValueKind.String => elem.GetString()?.Trim() ?? string.Empty,
                JsonValueKind.Number => elem.GetRawText(),
                _ => string.Empty
            };
        }
        return string.Empty;
    }

    private static double TryGetDouble(JsonElement obj, string propertyName)
    {
        if (obj.TryGetProperty(propertyName, out var elem))
        {
            if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDouble(out var d))
                return d;
            if (elem.ValueKind == JsonValueKind.String && double.TryParse(elem.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return 0;
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
        _vatsimLock.Dispose();
        _ivaoLock.Dispose();
    }
}
