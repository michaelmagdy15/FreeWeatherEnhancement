using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SkyWeave.Api;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Models;

namespace SkyWeave.Api.Tests;

public class TestWeatherDataProvider : IWeatherDataProvider
{
    public bool HasPositionFix { get; set; } = true;

    public Task<ApiStatus> GetStatusAsync()
    {
        return Task.FromResult(new ApiStatus
        {
            IsRunning = true,
            Version = "0.6.0",
            SimConnected = true,
            IsInjecting = true,
            CurrentStation = "KJFK",
            HasPositionFix = HasPositionFix,
            SequenceNumber = 1
        });
    }

    public Task<AircraftWeatherSnapshot?> GetAircraftSnapshotAsync()
    {
        if (!HasPositionFix) return Task.FromResult<AircraftWeatherSnapshot?>(null);

        var state = new WeatherState
        {
            StationId = "KJFK",
            Latitude = 40.6399,
            Longitude = -73.7787,
            TemperatureCelsius = 22.5,
            DewpointCelsius = 15.0,
            AltimeterHpa = 1015.0,
            VisibilityMeters = 16093.44,
            WindDirectionDegrees = 180,
            WindSpeedKnots = 12,
            FlightCategory = "VFR"
        };

        return Task.FromResult<AircraftWeatherSnapshot?>(new AircraftWeatherSnapshot
        {
            SnapshotId = "test-snapshot-123",
            SequenceNumber = 1,
            TimestampUtc = DateTime.UtcNow,
            SimConnected = true,
            IsInjecting = true,
            HasPositionFix = true,
            Latitude = 40.6399,
            Longitude = -73.7787,
            AltitudeFeet = 5000,
            StationId = "KJFK",
            State = state,
            Atis = new VatsimAtisInfo
            {
                IcaoId = "KJFK",
                AtisLetter = "B",
                AltimeterHpa = 1015.0,
                AltimeterInHg = 29.97,
                WindDirection = 180,
                WindSpeedKt = 12,
                RunwayInUse = "31L",
                RawText = "KJFK ATIS INFO BRAVO 1200Z 18012KT QNH 1015 RWY 31L"
            }
        });
    }

    public Task<WeatherState?> GetCurrentAircraftStateAsync()
    {
        if (!HasPositionFix) return Task.FromResult<WeatherState?>(null);

        return Task.FromResult<WeatherState?>(new WeatherState
        {
            StationId = "KJFK",
            Latitude = 40.6399,
            Longitude = -73.7787,
            TemperatureCelsius = 22.5,
            DewpointCelsius = 15.0,
            AltimeterHpa = 1015.0,
            VisibilityMeters = 16093.44,
            WindDirectionDegrees = 180,
            WindSpeedKnots = 12,
            FlightCategory = "VFR"
        });
    }

    public Task<WeatherState?> GetStateAsync(double? latitude = null, double? longitude = null, string? station = null)
    {
        if (latitude.HasValue && longitude.HasValue)
        {
            return Task.FromResult<WeatherState?>(new WeatherState
            {
                StationId = station ?? "CUSTOM",
                Latitude = latitude.Value,
                Longitude = longitude.Value,
                TemperatureCelsius = 22.5,
                DewpointCelsius = 15.0,
                AltimeterHpa = 1015.0,
                VisibilityMeters = 16093.44,
                WindDirectionDegrees = 180,
                WindSpeedKnots = 12,
                FlightCategory = "VFR"
            });
        }

        if (!string.IsNullOrWhiteSpace(station))
        {
            return Task.FromResult<WeatherState?>(new WeatherState
            {
                StationId = station,
                Latitude = 40.6399,
                Longitude = -73.7787,
                TemperatureCelsius = 22.5,
                DewpointCelsius = 15.0,
                AltimeterHpa = 1015.0,
                VisibilityMeters = 16093.44,
                WindDirectionDegrees = 180,
                WindSpeedKnots = 12,
                FlightCategory = "VFR"
            });
        }

        if (!HasPositionFix) return Task.FromResult<WeatherState?>(null);

        return Task.FromResult<WeatherState?>(new WeatherState
        {
            StationId = "KJFK",
            Latitude = 40.6399,
            Longitude = -73.7787,
            TemperatureCelsius = 22.5,
            DewpointCelsius = 15.0,
            AltimeterHpa = 1015.0,
            VisibilityMeters = 16093.44,
            WindDirectionDegrees = 180,
            WindSpeedKnots = 12,
            FlightCategory = "VFR"
        });
    }

    public Task<MetarData?> GetMetarAsync(double? latitude = null, double? longitude = null, string? station = null)
    {
        if (!latitude.HasValue && !longitude.HasValue && string.IsNullOrWhiteSpace(station) && !HasPositionFix)
        {
            return Task.FromResult<MetarData?>(null);
        }

        return Task.FromResult<MetarData?>(new MetarData
        {
            StationId = station ?? "KJFK",
            TemperatureCelsius = 22.5,
            FlightCategory = "VFR"
        });
    }

    public Task<List<WeatherHazard>?> GetHazardsAsync(double? latitude = null, double? longitude = null, string? station = null)
    {
        if (!latitude.HasValue && !longitude.HasValue && string.IsNullOrWhiteSpace(station) && !HasPositionFix)
        {
            return Task.FromResult<List<WeatherHazard>?>(null);
        }

        return Task.FromResult<List<WeatherHazard>?>(new List<WeatherHazard>
        {
            new() { Type = HazardType.TurbulenceSigmet, Description = "Moderate clear air turbulence", Severity = 0.5 }
        });
    }

    public Task<EfbSnapshot?> GetEfbSnapshotAsync(double? latitude = null, double? longitude = null, string? station = null)
    {
        bool isBriefing = (latitude.HasValue && longitude.HasValue) || !string.IsNullOrWhiteSpace(station);
        if (!isBriefing && !HasPositionFix)
        {
            return Task.FromResult<EfbSnapshot?>(null);
        }

        double effLat = latitude ?? 40.6399;
        double effLon = longitude ?? -73.7787;

        return Task.FromResult<EfbSnapshot?>(new EfbSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"),
            SequenceNumber = 1,
            TimestampUtc = DateTime.UtcNow,
            IsAircraftFollower = !isBriefing,
            HasPositionFix = true,
            StationId = station ?? "KJFK",
            RawMetar = "METAR KJFK 211200Z 18012KT 10SM CLR 22/15 A2997",
            ObservationTime = DateTime.UtcNow,
            FlightCategory = "VFR",
            TemperatureCelsius = 22.5,
            DewpointCelsius = 15.0,
            AltimeterHpa = 1015.0,
            VisibilityMeters = 16093.44,
            WindDirectionDegrees = 180,
            WindSpeedKnots = 12,
            Latitude = effLat,
            Longitude = effLon,
            CeilingFeet = 30000,
            FreezingLevelFeet = 14000,
            HumidityPercent = 60,
            WindsAloft = new List<WindLayer>
            {
                new() { AltitudeFeet = 3000, DirectionDegrees = 190, SpeedKnots = 15, TemperatureCelsius = 16 },
                new() { AltitudeFeet = 6000, DirectionDegrees = 200, SpeedKnots = 22, TemperatureCelsius = 10 }
            },
            Hazards = new List<WeatherHazard>
            {
                new() { Type = HazardType.TurbulenceSigmet, Description = "Moderate clear air turbulence", Severity = 0.5 }
            },
            RadarTimestamp = DateTime.UtcNow,
            RadarTileUrl = "https://tilecache.rainviewer.com/v2/radar/1690000000/256/2/1/1/2/1_1.png",
            SourceModelName = "HRRR CONUS 3 km",
            Atis = new VatsimAtisInfo
            {
                IcaoId = station ?? "KJFK",
                AtisLetter = "B",
                AltimeterHpa = 1015.0,
                AltimeterInHg = 29.97,
                WindDirection = 180,
                WindSpeedKt = 12,
                RunwayInUse = "31L",
                RawText = $"{station ?? "KJFK"} ATIS INFO BRAVO 1200Z 18012KT QNH 1015 RWY 31L"
            }
        });
    }

    public Task<VatsimAtisInfo?> GetAtisAsync(string? station = null)
    {
        var target = station ?? "KJFK";
        return Task.FromResult<VatsimAtisInfo?>(new VatsimAtisInfo
        {
            IcaoId = target,
            AtisLetter = "B",
            AltimeterHpa = 1015.0,
            AltimeterInHg = 29.97,
            WindDirection = 180,
            WindSpeedKt = 12,
            RunwayInUse = "31L",
            RawText = $"{target} ATIS INFO BRAVO 1200Z 18012KT QNH 1015 RWY 31L"
        });
    }

    public bool IsHistoricalMode { get; set; }
    public DateTime? HistoricalTargetUtc { get; set; }

    public Task SetHistoricalModeAsync(bool enabled, DateTime? targetUtc = null)
    {
        IsHistoricalMode = enabled;
        HistoricalTargetUtc = enabled ? (targetUtc ?? DateTime.UtcNow.Date.AddDays(-7).AddHours(12)) : null;
        return Task.CompletedTask;
    }

    public Task<WeatherState?> FetchHistoricalWeatherAsync(double latitude, double longitude, DateTime targetUtc, string? stationId = null)
    {
        return Task.FromResult<WeatherState?>(new WeatherState
        {
            StationId = stationId ?? "HIST",
            Latitude = latitude,
            Longitude = longitude,
            TemperatureCelsius = 16.0,
            AltimeterHpa = 1014.0,
            IsHistorical = true,
            HistoricalUtc = targetUtc,
            SourceModelName = "ERA5 Reanalysis (Historical)"
        });
    }

    public bool IsSandboxMode { get; set; }
    public SandboxWeatherScenario? CurrentSandboxScenario { get; set; }

    public Task SetSandboxModeAsync(bool enabled, SandboxWeatherScenario? scenario = null)
    {
        IsSandboxMode = enabled;
        CurrentSandboxScenario = enabled ? (scenario ?? SandboxWeatherScenario.CreateCrosswindLanding()) : null;
        return Task.CompletedTask;
    }

    public Task ApplySandboxScenarioAsync(SandboxWeatherScenario scenario)
    {
        IsSandboxMode = true;
        CurrentSandboxScenario = scenario;
        return Task.CompletedTask;
    }

    public Task ApplySandboxPresetAsync(string presetId)
    {
        IsSandboxMode = true;
        CurrentSandboxScenario = presetId.ToLowerInvariant() switch
        {
            "cat3_fog" or "fog" => SandboxWeatherScenario.CreateCat3Fog(),
            "crosswind" => SandboxWeatherScenario.CreateCrosswindLanding(),
            "supercell" or "thunderstorm" => SandboxWeatherScenario.CreateSupercellThunderstorm(),
            "mountain_wave" or "cat" => SandboxWeatherScenario.CreateMountainWaveCat(),
            "severe_icing" or "icing" => SandboxWeatherScenario.CreateSevereIcing(),
            "clear_calm" or "clear" => SandboxWeatherScenario.CreateClearAndCalm(),
            _ => SandboxWeatherScenario.CreateCrosswindLanding()
        };
        return Task.CompletedTask;
    }

    public List<AircraftTraffic> MockTraffic { get; set; } = new();
    public IReadOnlyList<AircraftTraffic> GetNearbyTraffic() => MockTraffic;
    public void SetTrafficSnapshot(IReadOnlyList<AircraftTraffic> traffic) => MockTraffic = traffic?.ToList() ?? new();
    public int TrafficCount => MockTraffic.Count;
    public bool HasWakeEncounter => MockTraffic.Any(t => t.IsInWakeZone);
}
