using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public enum SkyAnchorPhase
{
    EnRoute,
    DepartureHold,
    ArrivalHold,
    FinalFreeze,
    ManualFreeze
}

public class SkyAnchorState
{
    public SkyAnchorPhase Phase { get; set; } = SkyAnchorPhase.EnRoute;
    public bool IsFrozen { get; set; }
    public string? ActiveAnchorIcao { get; set; }
    public double? DistanceToOriginNm { get; set; }
    public double? DistanceToDestinationNm { get; set; }
    public double? AltitudeAglFeet { get; set; }
}

public class SkyAnchorManager
{
    private readonly StationFinder _stationFinder;
    private AirportData? _originAirport;
    private AirportData? _destinationAirport;
    private bool _manualFreeze;

    public bool DepartureHoldEnabled { get; set; } = true;
    public bool ArrivalHoldEnabled { get; set; } = true;
    public bool AutoFreezeOnApproach { get; set; } = true;

    public double DepartureHoldCeilingAglFeet { get; set; } = 4000.0;
    public double DepartureHoldMaxDistanceNm { get; set; } = 25.0;
    public double ArrivalHoldDistanceNm { get; set; } = 30.0;
    public double FinalFreezeDistanceNm { get; set; } = 5.0;
    public double FinalFreezeCeilingAglFeet { get; set; } = 1000.0;

    public AirportData? OriginAirport => _originAirport;
    public AirportData? DestinationAirport => _destinationAirport;

    public bool IsManualFrozen
    {
        get => _manualFreeze;
        set => _manualFreeze = value;
    }

    public SkyAnchorManager(StationFinder? stationFinder = null)
    {
        _stationFinder = stationFinder ?? new StationFinder();
    }

    public void SetRoute(string? originIcao, string? destinationIcao)
    {
        _originAirport = !string.IsNullOrWhiteSpace(originIcao)
            ? _stationFinder.FindStation(originIcao.Trim())
            : null;

        _destinationAirport = !string.IsNullOrWhiteSpace(destinationIcao)
            ? _stationFinder.FindStation(destinationIcao.Trim())
            : null;
    }

    public void SetFlightPlan(SimBriefPlan? plan)
    {
        if (plan == null)
        {
            _originAirport = null;
            _destinationAirport = null;
            return;
        }

        SetRoute(plan.Origin, plan.Destination);
    }

    public SkyAnchorState Evaluate(double aircraftLat, double aircraftLon, double aircraftAltFeet)
    {
        var state = new SkyAnchorState();

        if (_manualFreeze)
        {
            state.Phase = SkyAnchorPhase.ManualFreeze;
            state.IsFrozen = true;
            return state;
        }

        double? distDest = null;
        if (_destinationAirport != null)
        {
            distDest = RouteHazardAnalyzer.CalculateDistanceNm(
                aircraftLat, aircraftLon,
                _destinationAirport.Latitude, _destinationAirport.Longitude);
            state.DistanceToDestinationNm = distDest;
        }

        double? distOrig = null;
        if (_originAirport != null)
        {
            distOrig = RouteHazardAnalyzer.CalculateDistanceNm(
                aircraftLat, aircraftLon,
                _originAirport.Latitude, _originAirport.Longitude);
            state.DistanceToOriginNm = distOrig;
        }

        // Priority 1: Final Freeze on short final (within 5 NM and <= 1,000 ft AGL)
        if (AutoFreezeOnApproach && _destinationAirport != null && distDest.HasValue && distDest.Value <= FinalFreezeDistanceNm)
        {
            var agl = Math.Max(0, aircraftAltFeet - _destinationAirport.ElevationFeet);
            state.AltitudeAglFeet = agl;
            if (agl <= FinalFreezeCeilingAglFeet)
            {
                state.Phase = SkyAnchorPhase.FinalFreeze;
                state.IsFrozen = true;
                state.ActiveAnchorIcao = _destinationAirport.IcaoId;
                return state;
            }
        }

        // Priority 2: Arrival Hold (within 30 NM of destination)
        if (ArrivalHoldEnabled && _destinationAirport != null && distDest.HasValue && distDest.Value <= ArrivalHoldDistanceNm)
        {
            state.Phase = SkyAnchorPhase.ArrivalHold;
            state.IsFrozen = false;
            state.ActiveAnchorIcao = _destinationAirport.IcaoId;
            state.AltitudeAglFeet = Math.Max(0, aircraftAltFeet - _destinationAirport.ElevationFeet);
            return state;
        }

        // Priority 3: Departure Hold (<= 4,000 ft AGL from origin)
        if (DepartureHoldEnabled && _originAirport != null)
        {
            var agl = Math.Max(0, aircraftAltFeet - _originAirport.ElevationFeet);
            state.AltitudeAglFeet = agl;
            if ((distOrig == null || distOrig.Value <= DepartureHoldMaxDistanceNm) && agl <= DepartureHoldCeilingAglFeet)
            {
                state.Phase = SkyAnchorPhase.DepartureHold;
                state.IsFrozen = false;
                state.ActiveAnchorIcao = _originAirport.IcaoId;
                return state;
            }
        }

        // Priority 4: Standard EnRoute
        state.Phase = SkyAnchorPhase.EnRoute;
        state.IsFrozen = false;
        state.ActiveAnchorIcao = null;
        return state;
    }
}
