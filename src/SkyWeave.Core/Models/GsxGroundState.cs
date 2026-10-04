using System;

namespace SkyWeave.Core.Models;

public enum GsxDeicingState
{
    None = 0,
    TruckArriving = 1,
    Active = 2,
    Completed = 3,
    Aborted = 4
}

public enum GsxDeicingType
{
    None = 0,
    OneStep_TypeI = 1,
    TwoStep_TypeI_TypeIV = 2
}

public enum GsxBoardingState
{
    Inactive = 0,
    Requested = 1,
    Active = 2,
    Completed = 3
}

public enum GsxRefuelingState
{
    Inactive = 0,
    Arriving = 1,
    Active = 2,
    Completed = 3
}

public enum GsxPushbackState
{
    Inactive = 0,
    TugConnecting = 1,
    PinInsertedWaitBrake = 2,
    Pushing = 3,
    Completed = 4
}

public enum GsxRampSafetyLevel
{
    Normal = 0,
    Caution = 1,
    Warning = 2
}

/// <summary>
/// Snapshot of GSX Pro ground operations telemetry, deicing holdover time calculations,
/// and meteorological ramp safety advisory.
/// </summary>
public class GsxGroundState
{
    public bool IsOperating { get; set; }
    public GsxDeicingState DeicingState { get; set; } = GsxDeicingState.None;
    public GsxDeicingType DeicingType { get; set; } = GsxDeicingType.None;
    public DateTime? DeicingStartedAtUtc { get; set; }
    public DateTime? DeicingCompletedAtUtc { get; set; }

    public GsxBoardingState BoardingState { get; set; } = GsxBoardingState.Inactive;
    public int PassengersBoarded { get; set; }
    public int PassengersTotal { get; set; }
    public double BoardingPercent => PassengersTotal > 0 ? Math.Clamp((double)PassengersBoarded / PassengersTotal * 100.0, 0, 100) : 0;

    public GsxRefuelingState RefuelingState { get; set; } = GsxRefuelingState.Inactive;
    public bool CateringActive { get; set; }
    public GsxPushbackState PushbackState { get; set; } = GsxPushbackState.Inactive;

    // Meteorological Deicing & Holdover Time (HOT)
    public bool IsDeicingRequired { get; set; }
    public string RecommendedFluid { get; set; } = "None";
    public double EstimatedHoldoverDurationMinutes { get; set; }
    public DateTime? HoldoverExpiresAtUtc { get; set; }
    public TimeSpan? HoldoverTimeRemaining => HoldoverExpiresAtUtc.HasValue
        ? (HoldoverExpiresAtUtc.Value > DateTime.UtcNow ? HoldoverExpiresAtUtc.Value - DateTime.UtcNow : TimeSpan.Zero)
        : null;

    public string DeicingAdvisoryText { get; set; } = "OAT above +3°C — Deicing not required";
    public string HoldoverStatusText { get; set; } = "STANDBY";

    // Ramp Weather Safety
    public GsxRampSafetyLevel RampSafetyLevel { get; set; } = GsxRampSafetyLevel.Normal;
    public string RampSafetyAlertText { get; set; } = "NORMAL — Ramp conditions nominal";
}
