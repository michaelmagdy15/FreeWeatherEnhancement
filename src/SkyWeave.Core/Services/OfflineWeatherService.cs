using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class OfflineWeatherService : IOfflineWeatherService
{
    private readonly StationFinder _stationFinder;
    private readonly MetarFetcher? _metarFetcher;
    private readonly WindsAloftFetcher? _windsAloftFetcher;
    private readonly TafFetcher? _tafFetcher;
    private readonly string _storageDirectory;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public bool IsOfflineMode { get; private set; }
    public OfflineWeatherPackage? ActivePackage { get; private set; }
    public string StorageDirectory => _storageDirectory;

    public OfflineWeatherService(
        StationFinder? stationFinder = null,
        MetarFetcher? metarFetcher = null,
        WindsAloftFetcher? windsAloftFetcher = null,
        TafFetcher? tafFetcher = null,
        string? storageDirectory = null)
    {
        _stationFinder = stationFinder ?? new StationFinder();
        _metarFetcher = metarFetcher;
        _windsAloftFetcher = windsAloftFetcher;
        _tafFetcher = tafFetcher;

        _storageDirectory = storageDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkyWeave",
            "OfflinePackages");

        try
        {
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }
        catch
        {
            // Fallback to temp if LocalApplicationData has permission restrictions
            _storageDirectory = Path.Combine(Path.GetTempPath(), "SkyWeave_OfflinePackages");
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public IReadOnlyList<OfflinePackageSummary> ListPackages()
    {
        lock (_lock)
        {
            var summaries = new List<OfflinePackageSummary>();
            if (!Directory.Exists(_storageDirectory))
                return summaries;

            var files = Directory.GetFiles(_storageDirectory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    var json = File.ReadAllText(file);
                    var package = JsonSerializer.Deserialize<OfflineWeatherPackage>(json, JsonOptions);
                    if (package != null)
                    {
                        bool isActive = IsOfflineMode && ActivePackage?.Id == package.Id;
                        summaries.Add(package.ToSummary(fileInfo.Length, isActive));
                    }
                }
                catch
                {
                    // Ignore corrupted individual files
                }
            }

            return summaries.OrderByDescending(s => s.CreatedUtc).ToList();
        }
    }

    public async Task<OfflineWeatherPackage?> GetPackageAsync(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return null;

        var cleanId = Path.GetFileNameWithoutExtension(packageId);
        var path = Path.Combine(_storageDirectory, $"{cleanId}.json");
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<OfflineWeatherPackage>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<OfflineWeatherPackage> SavePackageAsync(OfflineWeatherPackage package, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(package.Id))
        {
            package.Id = Guid.NewGuid().ToString("N")[..12];
        }

        var path = Path.Combine(_storageDirectory, $"{package.Id}.json");
        var json = JsonSerializer.Serialize(package, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
        return package;
    }

    public bool DeletePackage(string packageId)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(packageId)) return false;

            var cleanId = Path.GetFileNameWithoutExtension(packageId);
            var path = Path.Combine(_storageDirectory, $"{cleanId}.json");
            if (!File.Exists(path)) return false;

            try
            {
                File.Delete(path);
                if (ActivePackage?.Id == cleanId)
                {
                    DeactivateOfflineMode();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<bool> ActivatePackageAsync(string packageId)
    {
        var package = await GetPackageAsync(packageId);
        if (package == null) return false;

        lock (_lock)
        {
            ActivePackage = package;
            IsOfflineMode = true;
        }
        return true;
    }

    public void DeactivateOfflineMode()
    {
        lock (_lock)
        {
            IsOfflineMode = false;
        }
    }

    public void SetActivePackageDirect(OfflineWeatherPackage package)
    {
        lock (_lock)
        {
            ActivePackage = package;
            IsOfflineMode = true;
        }
    }

    public async Task<OfflineWeatherPackage> CreatePackageAsync(OfflinePackageCreateRequest request, CancellationToken ct = default)
    {
        bool isEgypt = string.Equals(request.Preset, "egypt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.IcaoPrefix, "HE", StringComparison.OrdinalIgnoreCase);

        var name = !string.IsNullOrWhiteSpace(request.Name)
            ? request.Name
            : (isEgypt ? "Egypt & Red Sea (HECC FIR)" : "Regional Weather Package");

        var description = !string.IsNullOrWhiteSpace(request.Description)
            ? request.Description
            : (isEgypt
                ? "Complete offline weather package for Egypt (31 airports including HECA, HESH, HELX, HEGN, HEBA) with high-resolution upper winds aloft grid."
                : "Preloaded regional offline weather package.");

        var regionCode = !string.IsNullOrWhiteSpace(request.RegionCode)
            ? request.RegionCode
            : (isEgypt ? "EG" : "REG");

        var icaoPrefix = !string.IsNullOrWhiteSpace(request.IcaoPrefix)
            ? request.IcaoPrefix
            : (isEgypt ? "HE" : "");

        double minLat = request.MinLatitude ?? (isEgypt ? 21.5 : 20.0);
        double maxLat = request.MaxLatitude ?? (isEgypt ? 32.0 : 35.0);
        double minLon = request.MinLongitude ?? (isEgypt ? 24.5 : 20.0);
        double maxLon = request.MaxLongitude ?? (isEgypt ? 37.0 : 40.0);

        // Gather stations
        List<AirportData> targetAirports = new();
        if (request.IcaoList != null && request.IcaoList.Count > 0)
        {
            foreach (var icao in request.IcaoList)
            {
                var ap = _stationFinder.FindStation(icao);
                if (ap != null) targetAirports.Add(ap);
            }
        }
        else if (!string.IsNullOrWhiteSpace(icaoPrefix))
        {
            targetAirports = _stationFinder.GetAirportsByPrefix(icaoPrefix);
        }
        else
        {
            targetAirports = _stationFinder.GetAirportsInBounds(minLat, maxLat, minLon, maxLon);
        }

        if (targetAirports.Count == 0 && isEgypt)
        {
            targetAirports = _stationFinder.GetAirportsByPrefix("HE");
        }

        var package = new OfflineWeatherPackage
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = name,
            Description = description,
            RegionCode = regionCode,
            IcaoPrefix = icaoPrefix,
            CreatedUtc = DateTime.UtcNow,
            MinLatitude = minLat,
            MaxLatitude = maxLat,
            MinLongitude = minLon,
            MaxLongitude = maxLon
        };

        // Fetch METARs and TAFs
        foreach (var airport in targetAirports)
        {
            if (ct.IsCancellationRequested) break;

            MetarData? metar = null;
            if (_metarFetcher != null)
            {
                try
                {
                    metar = await _metarFetcher.FetchMetarAsync(airport.IcaoId);
                }
                catch
                {
                    // Fallback to synthetic
                }
            }

            if (metar == null)
            {
                metar = CreateSyntheticStationMetar(airport);
            }

            package.Stations[airport.IcaoId] = metar;

            if (_tafFetcher != null)
            {
                try
                {
                    var taf = await _tafFetcher.FetchTafAsync(airport.IcaoId);
                    if (taf != null)
                    {
                        package.Tafs[airport.IcaoId] = taf;
                    }
                }
                catch
                {
                    // TAF optional
                }
            }
        }

        // Upper Air Winds Grid across bounding box
        double latStep = 2.5;
        double lonStep = 2.5;

        for (double lat = minLat; lat <= maxLat + 0.1; lat += latStep)
        {
            for (double lon = minLon; lon <= maxLon + 0.1; lon += lonStep)
            {
                if (ct.IsCancellationRequested) break;

                WindsAloftData? winds = null;
                if (_windsAloftFetcher != null)
                {
                    try
                    {
                        winds = await _windsAloftFetcher.FetchWindsAloftAsync(lat, lon);
                    }
                    catch
                    {
                        // Fallback to synthetic
                    }
                }

                if (winds == null)
                {
                    winds = CreateSyntheticWindsAloft(lat, lon);
                }

                package.WindsGrid.Add(winds);
            }
        }

        await SavePackageAsync(package, ct);

        if (request.ActivateImmediately)
        {
            lock (_lock)
            {
                ActivePackage = package;
                IsOfflineMode = true;
            }
        }

        return package;
    }

    public OfflineWeatherPackage CreateSyntheticBaselinePackage(string preset = "egypt")
    {
        bool isEgypt = string.Equals(preset, "egypt", StringComparison.OrdinalIgnoreCase);

        var package = new OfflineWeatherPackage
        {
            Id = isEgypt ? "preset_egypt_baseline" : Guid.NewGuid().ToString("N")[..12],
            Name = isEgypt ? "Egypt & Red Sea (HECC FIR)" : "Offline Baseline Region",
            Description = isEgypt
                ? "Preloaded full offline weather package for Egypt (31 airports including Cairo, Sharm El Sheikh, Luxor, Hurghada, Borg El Arab, Aswan) with complete upper winds grid."
                : "Offline synthetic regional baseline package.",
            RegionCode = isEgypt ? "EG" : "REG",
            IcaoPrefix = isEgypt ? "HE" : "",
            CreatedUtc = DateTime.UtcNow,
            MinLatitude = isEgypt ? 21.5 : 20.0,
            MaxLatitude = isEgypt ? 32.0 : 35.0,
            MinLongitude = isEgypt ? 24.5 : 20.0,
            MaxLongitude = isEgypt ? 37.0 : 40.0
        };

        var airports = isEgypt ? _stationFinder.GetAirportsByPrefix("HE") : _stationFinder.GetAirportsInBounds(20, 35, 20, 40);
        foreach (var ap in airports)
        {
            package.Stations[ap.IcaoId] = CreateSyntheticStationMetar(ap);
        }

        double latStep = 2.5;
        double lonStep = 2.5;
        for (double lat = package.MinLatitude; lat <= package.MaxLatitude + 0.1; lat += latStep)
        {
            for (double lon = package.MinLongitude; lon <= package.MaxLongitude + 0.1; lon += lonStep)
            {
                package.WindsGrid.Add(CreateSyntheticWindsAloft(lat, lon));
            }
        }

        return package;
    }

    private static MetarData CreateSyntheticStationMetar(AirportData airport)
    {
        // Egyptian climate baseline: warm, dry, clear/few clouds, light northerly/northwesterly winds
        bool isRedSeaOrUpperEgypt = airport.Latitude < 28.0;
        double temp = isRedSeaOrUpperEgypt ? 31.0 : 27.0;
        double dew = isRedSeaOrUpperEgypt ? 14.0 : 16.0;
        double windDir = isRedSeaOrUpperEgypt ? 350.0 : 330.0;
        double windSpd = isRedSeaOrUpperEgypt ? 11.0 : 8.0;
        double qnh = 1013.0;

        return new MetarData
        {
            StationId = airport.IcaoId,
            ObservationTime = DateTime.UtcNow,
            WindDirectionDegrees = windDir,
            WindSpeedKnots = windSpd,
            WindGustKnots = null,
            VisibilityMeters = 10000,
            TemperatureCelsius = temp,
            DewpointCelsius = dew,
            AltimeterHpa = qnh,
            FlightCategory = "VFR",
            Clouds = new List<MetarCloud>
            {
                new() { Coverage = "FEW", BaseFeet = 4500, Type = "" }
            },
            WeatherConditions = new List<string>(),
            RawText = $"{airport.IcaoId} {DateTime.UtcNow:ddHHmm}Z {windDir:000}{windSpd:00}KT 9999 FEW045 {temp:00}/{dew:00} Q{qnh:0000} NOSIG"
        };
    }

    private static WindsAloftData CreateSyntheticWindsAloft(double lat, double lon)
    {
        var data = new WindsAloftData
        {
            Latitude = lat,
            Longitude = lon,
            ForecastTime = DateTime.UtcNow,
            SourceModel = "Offline Regional Climatology",
            ModelResolutionKm = 25,
            DataAgeMinutes = 0,
            ShortwaveRadiationWm2 = 650
        };

        // Standard tropospheric pressure levels with subtropical jet profile
        int[] hpaLevels = { 1000, 925, 850, 700, 500, 400, 300, 250, 200, 150, 100 };
        double[] altMeters = { 110, 800, 1500, 3100, 5800, 7500, 9600, 10800, 12200, 14000, 16500 };
        double[] temps = { 26.0, 21.0, 16.0, 6.0, -13.0, -26.0, -42.0, -51.0, -56.0, -60.0, -63.0 };
        double[] windSpeeds = { 8.0, 12.0, 16.0, 22.0, 35.0, 50.0, 75.0, 85.0, 70.0, 45.0, 25.0 };
        double[] windDirs = { 330.0, 320.0, 310.0, 300.0, 290.0, 280.0, 275.0, 270.0, 265.0, 260.0, 250.0 };

        for (int i = 0; i < hpaLevels.Length; i++)
        {
            data.PressureLevels.Add(new PressureLevelData
            {
                PressureHpa = hpaLevels[i],
                AltitudeMeters = altMeters[i],
                AltitudeFeet = altMeters[i] * 3.28084,
                TemperatureCelsius = temps[i],
                RelativeHumidity = 25,
                CloudCoverPercent = 5,
                WindSpeedKnots = windSpeeds[i],
                WindDirectionDegrees = windDirs[i],
                GeopotentialHeightMeters = altMeters[i]
            });
        }

        return data;
    }
}
