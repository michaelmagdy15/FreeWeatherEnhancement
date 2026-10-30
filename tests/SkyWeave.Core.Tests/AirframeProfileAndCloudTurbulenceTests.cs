using SkyWeave.Core.Builders;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class AirframeProfileAndCloudTurbulenceTests
{
    private static WeatherState TurbulentState() => new()
    {
        TurbulenceLayers = new List<TurbulenceLayer>
        {
            new() { BaseFeet = 0, TopFeet = 40000, Intensity = TurbulenceIntensity.Moderate, Type = TurbulenceType.Thermal }
        },
        TurbulenceIndex = 0.6
    };

    private static AirframePerturbation Step(AirframeProfile profile, double altitudeFeet, double weightPounds)
    {
        var physics = new AirframePhysicsCalculator();
        return physics.CalculateStep(
            weather: TurbulentState(),
            aircraftLatitude: 40,
            aircraftLongitude: -73,
            aircraftAltitudeFeet: altitudeFeet,
            aircraftHeadingDegrees: 0,
            aircraftWeightPounds: weightPounds,
            isOnGround: false,
            nearbyTraffic: null,
            intensityScale: 1.0,
            profile: profile,
            randomSample: 0.8);
    }

    [Fact]
    public void GaProfile_HitsHarderThanAirlinerInLowLevelChop()
    {
        var ga = Step(AirframeProfile.GeneralAviation, 2000, 3000);
        var airliner = Step(AirframeProfile.Airliner, 2000, 3000);

        Assert.True(Math.Abs(ga.RollRateRadPerSec) > Math.Abs(airliner.RollRateRadPerSec));
        Assert.True(Math.Abs(ga.VerticalVelocityFps) > Math.Abs(airliner.VerticalVelocityFps));
    }

    [Fact]
    public void AirlinerProfile_HitsHarderThanGaAtCruiseAltitude()
    {
        var ga = Step(AirframeProfile.GeneralAviation, 35000, 150000);
        var airliner = Step(AirframeProfile.Airliner, 35000, 150000);

        Assert.True(Math.Abs(airliner.RollRateRadPerSec) > Math.Abs(ga.RollRateRadPerSec));
    }

    [Fact]
    public void AutoProfile_LeavesMassDampingAlone()
    {
        Assert.Equal(1.0, AirframePhysicsCalculator.ComputeProfileFactor(AirframeProfile.Auto, 2000));
        Assert.Equal(1.0, AirframePhysicsCalculator.ComputeProfileFactor(AirframeProfile.Auto, 35000));
        Assert.Equal(1.15, AirframePhysicsCalculator.ComputeProfileFactor(AirframeProfile.GeneralAviation, 2000));
        Assert.Equal(0.70, AirframePhysicsCalculator.ComputeProfileFactor(AirframeProfile.GeneralAviation, 35000));
        Assert.Equal(0.80, AirframePhysicsCalculator.ComputeProfileFactor(AirframeProfile.Airliner, 2000));
        Assert.Equal(1.10, AirframePhysicsCalculator.ComputeProfileFactor(AirframeProfile.Airliner, 35000));
    }

    [Fact]
    public void Perturbations_StayClampedWithExtremeProfileAndScale()
    {
        var physics = new AirframePhysicsCalculator();
        var step = physics.CalculateStep(
            TurbulentState(), 40, -73, 3000, 0, 3000, false, null,
            intensityScale: 2.0, profile: AirframeProfile.GeneralAviation, randomSample: 1.0);

        Assert.InRange(step.RollRateRadPerSec, -0.38, 0.38);
        Assert.InRange(step.PitchRateRadPerSec, -0.16, 0.16);
        Assert.InRange(step.YawRateRadPerSec, -0.18, 0.18);
        Assert.InRange(step.VerticalVelocityFps, -42.0, 42.0);
    }

    [Fact]
    public void DeepStratiformLayer_ProducesEmbeddedTurbulence()
    {
        var clouds = new List<CloudLayer>
        {
            new()
            {
                BaseFeetAgl = 2000, TopFeetAgl = 12000,
                BaseMeters = 2000 * WeatherUnits.FeetToMeters,
                TopMeters = 12000 * WeatherUnits.FeetToMeters,
                Type = CloudType.NS, Density = 0.75
            }
        };

        var layers = new TurbulenceCalculator().CalculateTurbulenceLayers(new(), clouds, new(), 0);

        var layer = Assert.Single(layers);
        Assert.Equal(2000, layer.BaseFeet, 0);
        Assert.Equal(12000, layer.TopFeet, 0);
        Assert.Equal(TurbulenceIntensity.Moderate, layer.Intensity);
    }

    [Fact]
    public void ShallowOrThinCloud_DoesNotAddTurbulence()
    {
        var clouds = new List<CloudLayer>
        {
            new()
            {
                BaseFeetAgl = 5000, TopFeetAgl = 6500,
                BaseMeters = 5000 * WeatherUnits.FeetToMeters,
                TopMeters = 6500 * WeatherUnits.FeetToMeters,
                Type = CloudType.ST, Density = 0.9
            }
        };

        var layers = new TurbulenceCalculator().CalculateTurbulenceLayers(new(), clouds, new(), 0);

        Assert.Empty(layers);
    }

    [Fact]
    public void DeepDenseCumulonimbus_EscalatesToSevere()
    {
        var clouds = new List<CloudLayer>
        {
            new()
            {
                BaseFeetAgl = 3000, TopFeetAgl = 42000,
                BaseMeters = 3000 * WeatherUnits.FeetToMeters,
                TopMeters = 42000 * WeatherUnits.FeetToMeters,
                Type = CloudType.CB, Density = 0.9
            }
        };

        var layer = Assert.Single(new TurbulenceCalculator().CalculateTurbulenceLayers(new(), clouds, new(), 0));

        Assert.Equal(TurbulenceIntensity.Severe, layer.Intensity);
        Assert.Equal(TurbulenceType.Thermal, layer.Type);
    }
}
