using System.Text.Json;
using SkyWeave.Core.Models;
using SkyWeave.SimBridge;
using Xunit;

namespace SkyWeave.Core.Tests;

public class WeatherBridgeProtocolTests
{
    [Fact]
    public void CreateApplyMessage_ContainsVersionedStateAndWpr()
    {
        var state = new WeatherState
        {
            TemperatureCelsius = 18,
            WindSpeedKnots = 12,
            CloudLayers = new List<CloudLayer>()
        };

        var message = WeatherBridgeProtocol.CreateApplyMessage("abc123", state, "<wpr />");

        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("protocol").GetInt32());
        Assert.Equal("abc123", root.GetProperty("requestId").GetString());
        Assert.Equal("<wpr />", root.GetProperty("presetXml").GetString());
        Assert.Equal(18, root.GetProperty("state").GetProperty("temperatureCelsius").GetDouble());
    }

    [Fact]
    public void TryParseAcknowledgement_RejectsWrongProtocol()
    {
        var result = WeatherBridgeProtocol.TryParseAcknowledgement(
            "{\"protocol\":2,\"requestId\":\"abc\",\"accepted\":true}",
            out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseAcknowledgement_ParsesAcceptedMessage()
    {
        var result = WeatherBridgeProtocol.TryParseAcknowledgement(
            "{\"protocol\":1,\"type\":\"acknowledge\",\"requestId\":\"abc\",\"accepted\":true}",
            out var acknowledgement);

        Assert.True(result);
        Assert.Equal("abc", acknowledgement.RequestId);
        Assert.True(acknowledgement.Accepted);
    }
}
