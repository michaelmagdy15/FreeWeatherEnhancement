using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.App.ViewModels;

public partial class GsxGroundOpsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private bool _isGsxOperating;

    [ObservableProperty]
    private string _boardingStatusText = "INACTIVE";

    [ObservableProperty]
    private double _boardingPercent;

    [ObservableProperty]
    private string _passengersText = "0 / 0 PAX";

    [ObservableProperty]
    private string _refuelingStatusText = "INACTIVE";

    [ObservableProperty]
    private string _cateringStatusText = "INACTIVE";

    [ObservableProperty]
    private string _pushbackStatusText = "INACTIVE";

    [ObservableProperty]
    private string _deicingStatusText = "NOT REQUIRED";

    [ObservableProperty]
    private string _deicingFluidText = "None";

    [ObservableProperty]
    private string _deicingAdvisoryText = "OAT above +3°C — Deicing not required";

    [ObservableProperty]
    private string _holdoverTimerText = "STANDBY";

    [ObservableProperty]
    private string _holdoverBadgeColor = "#10B981";

    [ObservableProperty]
    private string _rampSafetyText = "NORMAL — Ramp conditions nominal";

    [ObservableProperty]
    private string _rampSafetyColor = "#10B981";

    [ObservableProperty]
    private string _lastDispatchedAction = "None";

    public GsxGroundOpsViewModel(MainViewModel main)
    {
        _main = main;
    }

    public void UpdateFromState(GsxGroundState state)
    {
        IsGsxOperating = state.IsOperating;

        // Boarding
        switch (state.BoardingState)
        {
            case GsxBoardingState.Requested:
                BoardingStatusText = "REQUESTED";
                break;
            case GsxBoardingState.Active:
                BoardingStatusText = $"BOARDING ({state.PassengersBoarded}/{state.PassengersTotal})";
                break;
            case GsxBoardingState.Completed:
                BoardingStatusText = "COMPLETED";
                break;
            default:
                BoardingStatusText = "INACTIVE";
                break;
        }

        BoardingPercent = state.BoardingPercent;
        PassengersText = $"{state.PassengersBoarded} / {state.PassengersTotal} PAX";

        // Refueling
        RefuelingStatusText = state.RefuelingState switch
        {
            GsxRefuelingState.Arriving => "TRUCK ARRIVING",
            GsxRefuelingState.Active => "FUELING ACTIVE",
            GsxRefuelingState.Completed => "COMPLETED",
            _ => "INACTIVE"
        };

        // Catering
        CateringStatusText = state.CateringActive ? "CATERING ACTIVE" : "INACTIVE";

        // Pushback
        PushbackStatusText = state.PushbackState switch
        {
            GsxPushbackState.TugConnecting => "TUG CONNECTING",
            GsxPushbackState.PinInsertedWaitBrake => "PIN INSERTED (RELEASE BRAKE)",
            GsxPushbackState.Pushing => "PUSHING BACK",
            GsxPushbackState.Completed => "COMPLETED",
            _ => "INACTIVE"
        };

        // Deicing & Holdover
        DeicingStatusText = state.DeicingState switch
        {
            GsxDeicingState.TruckArriving => "TRUCK ARRIVING",
            GsxDeicingState.Active => "DEICING UNDERWAY",
            GsxDeicingState.Completed => "COMPLETED",
            GsxDeicingState.Aborted => "ABORTED",
            _ => (state.IsDeicingRequired ? "REQUIRED PRIOR TO DEPARTURE" : "STANDBY")
        };

        DeicingFluidText = state.RecommendedFluid;
        DeicingAdvisoryText = state.DeicingAdvisoryText;
        HoldoverTimerText = state.HoldoverStatusText;

        HoldoverBadgeColor = state.HoldoverStatusText.Contains("EXPIRED")
            ? "#EF4444"
            : (state.HoldoverStatusText.Contains("REMAINING") ? "#38BDF8" : "#10B981");

        // Ramp Safety
        RampSafetyText = state.RampSafetyAlertText;
        RampSafetyColor = state.RampSafetyLevel switch
        {
            GsxRampSafetyLevel.Warning => "#EF4444",
            GsxRampSafetyLevel.Caution => "#F59E0B",
            _ => "#10B981"
        };
    }

    [RelayCommand]
    private async Task RequestService(string service)
    {
        LastDispatchedAction = $"Calling GSX {service.ToUpperInvariant()}...";
        _main.AppendLog($"GSX: Requesting {service} from GSX Pro...");
        await _main.RequestGsxServiceAsync(service);
        LastDispatchedAction = $"Dispatched {service.ToUpperInvariant()}";
    }
}
