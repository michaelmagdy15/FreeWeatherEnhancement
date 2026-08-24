using System.Text.Json;
using System.Text.Json.Serialization;
using SkyWeave.Core.Models;

namespace SkyWeave.SimBridge;

public static class WeatherBridgeProtocol
{
    public const int Version = 1;
    public const string ApplyEventName = "SkyWeave.Weather.Apply";
    public const string AcknowledgeEventName = "SkyWeave.Weather.Acknowledge";
    public const string HeartbeatEventName = "SkyWeave.Weather.Heartbeat";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string CreateApplyMessage(string requestId, WeatherState state, string wprXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(wprXml);

        return JsonSerializer.Serialize(new WeatherBridgeApplyMessage
        {
            Protocol = Version,
            RequestId = requestId,
            Type = "apply",
            PresetXml = wprXml,
            State = state
        }, JsonOptions);
    }

    public static bool TryParseAcknowledgement(string message, out WeatherBridgeAcknowledgement acknowledgement)
    {
        acknowledgement = new WeatherBridgeAcknowledgement();
        if (string.IsNullOrWhiteSpace(message))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<WeatherBridgeAcknowledgement>(message, JsonOptions);
            if (parsed == null || parsed.Protocol != Version || string.IsNullOrWhiteSpace(parsed.RequestId))
                return false;

            acknowledgement = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class WeatherBridgeApplyMessage
    {
        public int Protocol { get; init; }
        public string Type { get; init; } = string.Empty;
        public string RequestId { get; init; } = string.Empty;
        public string PresetXml { get; init; } = string.Empty;
        public WeatherState State { get; init; } = new();
    }
}

public sealed class WeatherBridgeAcknowledgement
{
    public int Protocol { get; init; }
    public string Type { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public bool Accepted { get; init; }
    public string? Error { get; init; }
}
