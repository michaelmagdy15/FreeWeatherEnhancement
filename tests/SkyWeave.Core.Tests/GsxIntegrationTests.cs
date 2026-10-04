using System;
using System.Collections.Generic;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class GsxIntegrationTests
{
    [Fact]
    public void HoldoverTimeCalculator_WarmWeather_ReportsDeicingNotRequired()
    {
        var calculator = new HoldoverTimeCalculator();
        var (isReq, fluid, minutes, advisory) = calculator.EvaluateDeicingNeed(
            temperatureCelsius: 18.0,
            dewpointCelsius: 10.0,
            precipType: PrecipitationType.None,
            precipRateMmPerHour: 0.0,
            visibilityMeters: 10000);

        Assert.False(isReq);
        Assert.Equal("None", fluid);
        Assert.Equal(0, minutes);
        Assert.Contains("above +3°C", advisory);
    }

    [Fact]
    public void HoldoverTimeCalculator_SnowFreezingTemp_RecommendsTypeIVWithValidDuration()
    {
        var calculator = new HoldoverTimeCalculator();
        var (isReq, fluid, minutes, advisory) = calculator.EvaluateDeicingNeed(
            temperatureCelsius: -2.0,
            dewpointCelsius: -3.0,
            precipType: PrecipitationType.Snow,
            precipRateMmPerHour: 1.0,
            visibilityMeters: 3000);

        Assert.True(isReq);
        Assert.Contains("Type IV", fluid);
        Assert.True(minutes >= 30.0);
        Assert.Contains("Snow", advisory);
    }

    [Fact]
    public void HoldoverTimeCalculator_FreezingFog_RecommendsDeicing()
    {
        var calculator = new HoldoverTimeCalculator();
        var (isReq, fluid, minutes, advisory) = calculator.EvaluateDeicingNeed(
            temperatureCelsius: -1.0,
            dewpointCelsius: -1.0,
            precipType: PrecipitationType.None,
            precipRateMmPerHour: 0.0,
            visibilityMeters: 400);

        Assert.True(isReq);
        Assert.Contains("Type IV", fluid);
        Assert.Contains("Freezing Fog", advisory);
    }

    [Fact]
    public void HoldoverTimeCalculator_RampSafety_EvaluatesWindAndLightningLimits()
    {
        var calculator = new HoldoverTimeCalculator();

        // 1. Normal conditions
        var (level1, msg1) = calculator.EvaluateRampSafety(windSpeedKnots: 10, windGustKnots: 15, closestLightningDistanceNm: null);
        Assert.Equal(GsxRampSafetyLevel.Normal, level1);
        Assert.Contains("NORMAL", msg1);

        // 2. High wind caution (catering)
        var (level2, msg2) = calculator.EvaluateRampSafety(windSpeedKnots: 20, windGustKnots: 38, closestLightningDistanceNm: 25.0);
        Assert.Equal(GsxRampSafetyLevel.Caution, level2);
        Assert.Contains("WIND CAUTION", msg2);

        // 3. Lightning ramp freeze (< 5 NM)
        var (level3, msg3) = calculator.EvaluateRampSafety(windSpeedKnots: 15, windGustKnots: 20, closestLightningDistanceNm: 3.5);
        Assert.Equal(GsxRampSafetyLevel.Warning, level3);
        Assert.Contains("RAMP FREEZE", msg3);
    }

    [Fact]
    public void GsxService_TelemetryUpdate_UpdatesBoardingAndDeicingState()
    {
        var service = new GsxService();

        var weather = new WeatherState
        {
            TemperatureCelsius = -2.0,
            DewpointCelsius = -3.0,
            Precipitation = PrecipitationType.Snow,
            PrecipitationRate = 1.0,
            VisibilityMeters = 2000,
            WindSpeedKnots = 12,
            WindGustKnots = 18
        };

        service.UpdateFromBridge(
            isOperating: true,
            deicingStateRaw: (int)GsxDeicingState.Active,
            deicingTypeRaw: (int)GsxDeicingType.TwoStep_TypeI_TypeIV,
            boardingStateRaw: (int)GsxBoardingState.Active,
            passengersBoarded: 142,
            passengersTotal: 186,
            refuelingStateRaw: (int)GsxRefuelingState.Active,
            cateringStateRaw: 1,
            pushbackStateRaw: (int)GsxPushbackState.Inactive,
            currentWeather: weather,
            closestLightningDistNm: null);

        var state = service.CurrentState;
        Assert.True(state.IsOperating);
        Assert.Equal(GsxDeicingState.Active, state.DeicingState);
        Assert.Equal(GsxBoardingState.Active, state.BoardingState);
        Assert.Equal(142, state.PassengersBoarded);
        Assert.Equal(186, state.PassengersTotal);
        Assert.True(state.BoardingPercent > 70.0);
        Assert.True(state.IsDeicingRequired);
        Assert.Contains("DEICING IN PROGRESS", state.HoldoverStatusText);
        Assert.True(state.CateringActive);
    }
}
