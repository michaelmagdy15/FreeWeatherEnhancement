using SkyWeave.Core.Decoders;
using Xunit;

namespace SkyWeave.Core.Tests;

public class MetarDecoderRawTests
{
    private readonly MetarDecoder _decoder = new();

    [Fact]
    public void DecodeRaw_TgftpKjfkSample_ParsesAllFields()
    {
        var raw = "KJFK 172151Z 04009KT 10SM FEW020 FEW160 BKN250 27/21 A2981 RMK AO2 SLP093 T02720211 $";

        var data = _decoder.DecodeRaw(raw);

        Assert.Equal("KJFK", data.StationId);
        Assert.Equal(40, data.WindDirectionDegrees);
        Assert.Equal(9, data.WindSpeedKnots);
        Assert.Equal(16093.44, data.VisibilityMeters, 2);
        Assert.Equal(27, data.TemperatureCelsius);
        Assert.Equal(21, data.DewpointCelsius);
        Assert.Equal(1009.5, data.AltimeterHpa, 1);
        Assert.Equal(3, data.Clouds.Count);
        Assert.Equal("BKN", data.Clouds[2].Coverage);
        Assert.Equal(25000, data.Clouds[2].BaseFeet);
        Assert.Equal("VFR", data.FlightCategory);
        Assert.Equal(21, data.ObservationTime.Hour);
        Assert.Equal(51, data.ObservationTime.Minute);
    }

    [Fact]
    public void DecodeRaw_VatsimKjfkSample_Parses()
    {
        var raw = "KJFK 172151Z 04009KT 10SM FEW020 FEW160 BKN250 27/21 A2981 RMK AO2 SLP093 T02720211 $";

        var data = _decoder.DecodeRaw(raw);

        Assert.Equal("KJFK", data.StationId);
        Assert.Equal("VFR", data.FlightCategory);
    }

    [Fact]
    public void DecodeRaw_NegativeTempsAndQnh_Parses()
    {
        var raw = "EDDF 181250Z 25015G28KT 9999 BKN015 M05/M08 Q1012 NOSIG";

        var data = _decoder.DecodeRaw(raw);

        Assert.Equal("EDDF", data.StationId);
        Assert.Equal(-5, data.TemperatureCelsius);
        Assert.Equal(-8, data.DewpointCelsius);
        Assert.Equal(1012, data.AltimeterHpa);
        Assert.Equal(28, data.WindGustKnots);
        Assert.Equal(15, data.WindSpeedKnots);
        Assert.Equal(9999, data.VisibilityMeters, 0);
        Assert.Equal("MVFR", data.FlightCategory);
    }

    [Fact]
    public void DecodeRaw_FractionalVisibility_Parses()
    {
        var raw = "KBOS 181250Z 25005KT 1 1/2SM BR OVC005 10/09 A2995";

        var data = _decoder.DecodeRaw(raw);

        Assert.Equal(2414.0, data.VisibilityMeters, 0);
        Assert.Equal("OVC", data.Clouds[0].Coverage);
        Assert.Equal(500, data.Clouds[0].BaseFeet);
        Assert.Equal("IFR", data.FlightCategory);
    }

    [Fact]
    public void DecodeRaw_VvAndGusts_Parses()
    {
        var raw = "KMSP 181250Z 18012G22KT 3SM -SN VV002 00/M02 A2977";

        var data = _decoder.DecodeRaw(raw);

        Assert.Equal(22, data.WindGustKnots);
        Assert.Equal("VV", data.Clouds[0].Coverage);
        Assert.Equal(0, data.Clouds[0].BaseFeet);
        Assert.Equal(0, data.TemperatureCelsius);
        Assert.Equal(-2, data.DewpointCelsius);
        Assert.Equal("LIFR", data.FlightCategory);
    }

    [Fact]
    public void DecodeRaw_VrbWind_BecomesZeroDirection()
    {
        var raw = "CYUL 181250Z VRB03KT 15SM SKC 12/08 A3010";

        var data = _decoder.DecodeRaw(raw);

        Assert.Equal(0, data.WindDirectionDegrees);
        Assert.Equal(3, data.WindSpeedKnots);
        Assert.Empty(data.Clouds);
        Assert.Equal("VFR", data.FlightCategory);
    }

    [Fact]
    public void DecodeRaw_P6Sm_GetsTenMiles()
    {
        var raw = "KSFO 181250Z 29005KT P6SM FEW030 SCT200 18/12 A3001";

        var data = _decoder.DecodeRaw(raw);

        Assert.Equal(16093.44, data.VisibilityMeters, 0);
        Assert.Equal("VFR", data.FlightCategory);
    }
}