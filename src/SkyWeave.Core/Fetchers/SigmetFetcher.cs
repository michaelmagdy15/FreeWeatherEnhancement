using System.Net.Http.Json;
using System.Text.Json;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Fetchers;

public class SigmetFetcher
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://aviationweather.gov/api/data";

    public SigmetFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<List<WeatherHazard>?> FetchSigmetsAsync()
    {
        return FetchRetry.WithRetryAsync(FetchInternalAsync, maxRetries: 3);
    }

    private async Task<List<WeatherHazard>?> FetchInternalAsync()
    {
        try
        {
            var url = $"{BaseUrl}/airsigmet?format=json";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.GetFromJsonAsync<JsonElement[]>(url, cts.Token);
            
            if (response == null)
                return new List<WeatherHazard>();

            return response.Select(ParseSigmet).Where(h => h != null).Cast<WeatherHazard>().ToList();
        }
        catch (OperationCanceledException)
        {
            return new List<WeatherHazard>();
        }
        catch (HttpRequestException)
        {
            return new List<WeatherHazard>();
        }
    }

    private WeatherHazard? ParseSigmet(JsonElement sigmet)
    {
        try
        {
            var hazard = new WeatherHazard
            {
                Id = sigmet.TryGetProperty("hazardId", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                Description = sigmet.TryGetProperty("hazardType", out var type) ? type.GetString() ?? string.Empty : string.Empty
            };

            if (sigmet.TryGetProperty("hazardSig", out var sig))
            {
                var sigStr = sig.GetString()?.ToLower();
                hazard.Severity = sigStr switch
                {
                    "severe" => 1.0,
                    "moderate" => 0.6,
                    "light" => 0.3,
                    _ => 0.5
                };
            }

            if (sigmet.TryGetProperty("validTimeFrom", out var from))
            {
                if (DateTime.TryParse(from.GetString(), out var time))
                    hazard.ValidFrom = time;
            }

            if (sigmet.TryGetProperty("validTimeTo", out var to))
            {
                if (DateTime.TryParse(to.GetString(), out var time))
                    hazard.ValidTo = time;
            }

            if (sigmet.TryGetProperty("hazardType", out var hType))
            {
                var typeStr = hType.GetString()?.ToLower();
                hazard.Type = typeStr switch
                {
                    "conv" => HazardType.ConvectiveSigmet,
                    "turb" => HazardType.TurbulenceSigmet,
                    "ice" => HazardType.IcingSigmet,
                    "va" => HazardType.VolcanicAshSigmet,
                    _ => HazardType.Sigmet
                };
            }

            return hazard;
        }
        catch
        {
            return null;
        }
    }
}
