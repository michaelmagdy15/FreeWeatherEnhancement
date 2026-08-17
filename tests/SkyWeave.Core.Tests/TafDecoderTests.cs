using SkyWeave.Core.Decoders;
using Xunit;

namespace SkyWeave.Core.Tests;

public class TafDecoderTests
{
    private const string SampleTaf =
        "TAF KJFK 171733Z 1718/0124 31014G22KT P6SM SCT030 BKN120 " +
        "FM191200 30012KT P6SM SCT025 BKN080 " +
        "TEMPO 1918/1922 3SM TSRA BKN015CB " +
        "BECMG 2100/2103 VRB05KT P6SM SKC " +
        "PROB30 2208/2212 1/2SM FG VV002";

    [Fact]
    public void Decode_ParsesStationWindCloudsAndCategory()
    {
        var taf = new TafDecoder().Decode(SampleTaf);

        Assert.Equal("KJFK", taf.StationId);
        Assert.Equal(310, taf.WindDirectionDegrees);
        Assert.Equal(14, taf.WindSpeedKnots);
        Assert.Equal(22, taf.WindGustKnots);
        Assert.Contains(taf.Clouds, c => c.Coverage == "SCT" && c.BaseFeet == 3000);
        Assert.Contains(taf.Clouds, c => c.Coverage == "BKN" && c.BaseFeet == 12000);
        Assert.Equal("VFR", taf.FlightCategory);
    }

    [Fact]
    public void Decode_ChangeGroups_DetectsFmTempoBecmgAndProb()
    {
        var groups = new TafDecoder().DecodeChangeGroups(SampleTaf);

        Assert.Equal(4, groups.Count);
        Assert.Equal("FM", groups[0].Type);
        Assert.Equal("TEMPO", groups[1].Type);
        Assert.Equal("BECMG", groups[2].Type);
        Assert.Equal("PROB", groups[3].Type);
        Assert.Equal(30, groups[3].Probability);
    }

    [Fact]
    public void Decode_ChangeGroups_ParsesValidityWindows()
    {
        var groups = new TafDecoder().DecodeChangeGroups(SampleTaf);

        Assert.NotNull(groups[0].ValidFrom);
        Assert.NotNull(groups[1].ValidFrom);
        Assert.NotNull(groups[1].ValidTo);
        Assert.Equal(19, groups[1].ValidFrom!.Value.Hour);
        Assert.Equal(19, groups[1].ValidTo!.Value.Hour);
        Assert.Equal(22, groups[1].ValidTo!.Value.Minute);
        Assert.Equal(21, groups[2].ValidFrom!.Value.Hour);
    }

    [Fact]
    public void Decode_ChangeGroups_ParsesPerGroupWeather()
    {
        var groups = new TafDecoder().DecodeChangeGroups(SampleTaf);

        Assert.Equal(300, groups[0].WindDirectionDegrees);
        Assert.Equal(12, groups[0].WindSpeedKnots);
        Assert.Contains(groups[1].WeatherConditions, w => w.Contains("TS"));
        Assert.Contains(groups[1].Clouds, c => c.Coverage == "BKN" && c.BaseFeet == 1500);
        Assert.Equal("MVFR", groups[1].FlightCategory);
        Assert.Equal("VFR", groups[2].FlightCategory);
        Assert.Equal("LIFR", groups[3].FlightCategory);
    }

    [Fact]
    public void Decode_ChangeGroups_EmptyInput_ReturnsEmptyList()
    {
        var groups = new TafDecoder().DecodeChangeGroups("TAF KJFK 171733Z 1718/0124 31014KT P6SM SKC");

        Assert.Empty(groups);
    }
}