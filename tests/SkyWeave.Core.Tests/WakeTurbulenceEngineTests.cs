using SkyWeave.Core.Builders;
using SkyWeave.Core.Models;
using Xunit;

namespace SkyWeave.Core.Tests;

public class WakeTurbulenceEngineTests
{
    private const double AircraftLatitude = 40.0;
    private const double AircraftLongitude = -74.0;
    private const double AircraftAltitudeFeet = 5000;

    private readonly WakeTurbulenceEngine _engine = new();

    private static AircraftTraffic CreateTraffic(string callsign, double latitudeOffsetDeg,
        double longitudeOffsetDeg = 0, double altitudeFeet = AircraftAltitudeFeet,
        AircraftWeightClass weightClass = AircraftWeightClass.Heavy)
    {
        return new AircraftTraffic
        {
            Callsign = callsign,
            Latitude = AircraftLatitude + latitudeOffsetDeg,
            Longitude = AircraftLongitude + longitudeOffsetDeg,
            AltitudeFeet = altitudeFeet,
            HeadingDegrees = 0,
            SpeedKnots = 250,
            GroundSpeedKnots = 250,
            WeightClass = weightClass,
            OnGround = false
        };
    }

    [Fact]
    public void CalculateWakeLayers_EmptyTrafficAndAirportFarAway_ReturnsEmpty()
    {
        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, AircraftAltitudeFeet,
            new List<AircraftTraffic>(), new List<WindLayer>(), 40.5, -74.5, 50, 0.1);

        Assert.Empty(layers);
    }

    [Fact]
    public void CalculateWakeLayers_HeavyAircraftTwoNmAheadSameAltitude_ReturnsWake()
    {
        // Traffic (leader) is 2 nm ahead of us (flying North heading 000)
        var traffic = new List<AircraftTraffic> { CreateTraffic("HEAVY1", 2.0 / 60.0) };
        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, AircraftAltitudeFeet,
            traffic, new List<WindLayer>(), 40.5, -74.5, 50, 0.1);

        Assert.NotEmpty(layers);
        Assert.Equal(TurbulenceType.Wake, layers[0].Type);
        Assert.True(layers[0].Intensity >= TurbulenceIntensity.Light);
    }

    [Fact]
    public void CalculateWakeLayers_AircraftBehind_ReturnsNoWake()
    {
        // Traffic is behind us flying North, so its wake is behind it, not in front of it
        var traffic = new List<AircraftTraffic> { CreateTraffic("BEHIND1", -2.0 / 60.0) };
        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, AircraftAltitudeFeet,
            traffic, new List<WindLayer>(), 40.5, -74.5, 50, 0.1);

        Assert.Empty(layers);
    }

    [Fact]
    public void CalculateWakeLayers_AircraftTenNmAhead_ReturnsNoWake()
    {
        var traffic = new List<AircraftTraffic> { CreateTraffic("FAR1", 10.0 / 60.0) };
        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, AircraftAltitudeFeet,
            traffic, new List<WindLayer>(), 40.5, -74.5, 50, 0.1);

        Assert.Empty(layers);
    }

    [Fact]
    public void CalculateWakeLayers_AltitudeDifferenceThreeThousandFeet_ReturnsNoWake()
    {
        var traffic = new List<AircraftTraffic>
        {
            CreateTraffic("HIGH1", 2.0 / 60.0, altitudeFeet: AircraftAltitudeFeet + 3000)
        };
        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, AircraftAltitudeFeet,
            traffic, new List<WindLayer>(), 40.5, -74.5, 50, 0.1);

        Assert.Empty(layers);
    }

    [Fact]
    public void CalculateWakeLayers_CrossTrackThreeNm_ReturnsNoWake()
    {
        var traffic = new List<AircraftTraffic>
        {
            CreateTraffic("SIDE1", 2.0 / 60.0, longitudeOffsetDeg: 3.0 / 60.0)
        };
        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, AircraftAltitudeFeet,
            traffic, new List<WindLayer>(), 40.5, -74.5, 50, 0.1);

        Assert.Empty(layers);
    }

    [Fact]
    public void CalculateWakeIntensityFactor_OrderingAndValues()
    {
        var super = _engine.CalculateWakeIntensityFactor(AircraftWeightClass.Super);
        var heavy = _engine.CalculateWakeIntensityFactor(AircraftWeightClass.Heavy);
        var medium = _engine.CalculateWakeIntensityFactor(AircraftWeightClass.Medium);
        var light = _engine.CalculateWakeIntensityFactor(AircraftWeightClass.Light);
        var unknown = _engine.CalculateWakeIntensityFactor(AircraftWeightClass.Unknown);

        Assert.True(super >= heavy);
        Assert.True(heavy >= medium);
        Assert.True(medium >= light);
        Assert.Equal(1.0, super);
        Assert.Equal(0.85, heavy);
        Assert.Equal(0.55, medium);
        Assert.Equal(0.25, light);
        Assert.Equal(0.4, unknown);
    }

    [Fact]
    public void CalculateWakeLayers_AirportCorridorHighDensity_ReturnsWake()
    {
        var traffic = new List<AircraftTraffic> { CreateTraffic("APPROACH1", -2.0 / 60.0, altitudeFeet: 3000) };
        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, 3000,
            traffic, new List<WindLayer>(), AircraftLatitude, AircraftLongitude, 5, 0.6);

        Assert.NotEmpty(layers);
        Assert.Equal(TurbulenceType.Wake, layers[0].Type);
    }

    [Fact]
    public void CalculateWakeLayers_AirportCorridorLowDensity_ReturnsEmpty()
    {
        var traffic = new List<AircraftTraffic> { CreateTraffic("APPROACH2", -2.0 / 60.0, altitudeFeet: 3000) };
        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, 3000,
            traffic, new List<WindLayer>(), AircraftLatitude, AircraftLongitude, 5, 0.05);

        Assert.Empty(layers);
    }

    [Fact]
    public void Constants_MatchAgreedContract()
    {
        Assert.Equal(15.0, WakeTurbulenceEngine.MaxTrafficRangeNm);
        Assert.Equal(0.5, WakeTurbulenceEngine.MinBehindDistanceNm);
        Assert.Equal(8.0, WakeTurbulenceEngine.MaxBehindDistanceNm);
        Assert.Equal(1500.0, WakeTurbulenceEngine.AltitudeToleranceFeet);
        Assert.Equal(1.5, WakeTurbulenceEngine.CrossTrackMaxNm);
    }

    [Fact]
    public void CalculateWakeLayers_PopulatesDistanceBearingAltitudeDelta_AndSetsWakeZone()
    {
        var target = CreateTraffic("LEADER", 2.0 / 60.0, altitudeFeet: AircraftAltitudeFeet - 200, weightClass: AircraftWeightClass.Super);
        var traffic = new List<AircraftTraffic> { target };

        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, AircraftAltitudeFeet,
            traffic, new List<WindLayer>(), 40.5, -74.5, 50, 0.1);

        Assert.NotEmpty(layers);
        Assert.True(target.IsInWakeZone);
        Assert.True(target.DistanceNm > 1.8 && target.DistanceNm < 2.2);
        Assert.Equal(-200.0, target.AltitudeDeltaFeet);
        Assert.True(target.RelativeBearingDegrees >= 0 && target.RelativeBearingDegrees <= 360);
    }

    [Fact]
    public void CalculateWakeLayers_GroundTraffic_PopulatesDistanceBearingButProducesNoWake()
    {
        var groundTarget = CreateTraffic("TAXI1", 1.0 / 60.0, altitudeFeet: 50);
        groundTarget.OnGround = true;
        var traffic = new List<AircraftTraffic> { groundTarget };

        var layers = _engine.CalculateWakeLayers(AircraftLatitude, AircraftLongitude, 50,
            traffic, new List<WindLayer>(), 40.5, -74.5, 50, 0.1);

        Assert.Empty(layers);
        Assert.False(groundTarget.IsInWakeZone);
        Assert.True(groundTarget.DistanceNm > 0.8 && groundTarget.DistanceNm < 1.2);
    }
}