using System;
using System.Threading.Tasks;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

/// <summary>
/// Manages GSX Pro ground operations telemetry, deicing holdover time calculations,
/// and dispatching ground service requests to MSFS.
/// </summary>
public class GsxService
{
    private readonly HoldoverTimeCalculator _calculator = new();
    private readonly object _stateLock = new();

    public GsxGroundState CurrentState { get; private set; } = new();

    public event EventHandler<GsxGroundState>? StateUpdated;
    public event Func<string, Task>? ServiceRequested;

    /// <summary>
    /// Updates GSX state with raw telemetry received from the simulator bridge (L:Vars).
    /// </summary>
    public void UpdateFromBridge(
        bool isOperating,
        int deicingStateRaw,
        int deicingTypeRaw,
        int boardingStateRaw,
        int passengersBoarded,
        int passengersTotal,
        int refuelingStateRaw,
        int cateringStateRaw,
        int pushbackStateRaw,
        WeatherState? currentWeather,
        double? closestLightningDistNm)
    {
        lock (_stateLock)
        {
            var deicingState = (GsxDeicingState)Math.Clamp(deicingStateRaw, 0, 4);
            var deicingType = (GsxDeicingType)Math.Clamp(deicingTypeRaw, 0, 2);
            var boardingState = (GsxBoardingState)Math.Clamp(boardingStateRaw, 0, 3);
            var refuelingState = (GsxRefuelingState)Math.Clamp(refuelingStateRaw, 0, 3);
            var pushbackState = (GsxPushbackState)Math.Clamp(pushbackStateRaw, 0, 4);

            var previousDeicingState = CurrentState.DeicingState;

            CurrentState.IsOperating = isOperating;
            CurrentState.DeicingState = deicingState;
            CurrentState.DeicingType = deicingType;
            CurrentState.BoardingState = boardingState;
            CurrentState.PassengersBoarded = passengersBoarded;
            CurrentState.PassengersTotal = passengersTotal;
            CurrentState.RefuelingState = refuelingState;
            CurrentState.CateringActive = cateringStateRaw > 0;
            CurrentState.PushbackState = pushbackState;

            // Track Deicing Transition & Timestamps
            if (previousDeicingState != GsxDeicingState.Active && deicingState == GsxDeicingState.Active)
            {
                CurrentState.DeicingStartedAtUtc = DateTime.UtcNow;
            }
            else if (previousDeicingState == GsxDeicingState.Active && deicingState == GsxDeicingState.Completed)
            {
                CurrentState.DeicingCompletedAtUtc = DateTime.UtcNow;
                if (CurrentState.EstimatedHoldoverDurationMinutes > 0)
                {
                    CurrentState.HoldoverExpiresAtUtc = DateTime.UtcNow.AddMinutes(CurrentState.EstimatedHoldoverDurationMinutes);
                }
            }

            // Meteorological Evaluation
            if (currentWeather != null)
            {
                var (isReq, fluid, hotMin, advisory) = _calculator.EvaluateDeicingNeed(
                    currentWeather.TemperatureCelsius,
                    currentWeather.DewpointCelsius,
                    currentWeather.Precipitation,
                    currentWeather.PrecipitationRate,
                    currentWeather.VisibilityMeters);

                CurrentState.IsDeicingRequired = isReq;
                CurrentState.RecommendedFluid = fluid;
                CurrentState.EstimatedHoldoverDurationMinutes = hotMin;
                CurrentState.DeicingAdvisoryText = advisory;

                // Ramp Safety
                var (level, alertMsg) = _calculator.EvaluateRampSafety(
                    currentWeather.WindSpeedKnots,
                    currentWeather.WindGustKnots ?? currentWeather.WindSpeedKnots,
                    closestLightningDistNm);

                CurrentState.RampSafetyLevel = level;
                CurrentState.RampSafetyAlertText = alertMsg;
            }

            // Update Holdover Status Text
            if (CurrentState.HoldoverExpiresAtUtc.HasValue)
            {
                var remaining = CurrentState.HoldoverTimeRemaining ?? TimeSpan.Zero;
                if (remaining.TotalSeconds <= 0)
                {
                    CurrentState.HoldoverStatusText = "EXPIRED (RETURN TO PAD)";
                }
                else
                {
                    CurrentState.HoldoverStatusText = $"{remaining.Minutes:D2}:{remaining.Seconds:D2} REMAINING";
                }
            }
            else if (CurrentState.DeicingState == GsxDeicingState.Active)
            {
                CurrentState.HoldoverStatusText = "DEICING IN PROGRESS";
            }
            else
            {
                CurrentState.HoldoverStatusText = CurrentState.IsDeicingRequired ? "REQUIRED PRIOR TO DEPARTURE" : "NOT REQUIRED";
            }
        }

        StateUpdated?.Invoke(this, CurrentState);
    }

    /// <summary>
    /// Dispatches a ground service request (e.g. "deicing", "boarding", "pushback", "catering", "refueling").
    /// </summary>
    public async Task RequestServiceAsync(string service)
    {
        if (ServiceRequested != null)
        {
            await ServiceRequested.Invoke(service);
        }
    }
}
