using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;

namespace SkyWeave.App.ViewModels;

public class SimBriefWaypointViewModel
{
    public string Identifier { get; set; } = string.Empty;
    public string Altitude { get; set; } = string.Empty;
    public string Wind { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
}

public partial class FlightPlanViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly WeatherEngine _weatherEngine;
    private SimBriefPlan? _currentPlan;

    [ObservableProperty]
    private string _pilotId = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasPlan;

    [ObservableProperty]
    private string _statusMessage = "Enter SimBrief Pilot ID or Username to import latest OFP.";

    [ObservableProperty]
    private string _flightNumber = "---";

    [ObservableProperty]
    private string _origin = "----";

    [ObservableProperty]
    private string _destination = "----";

    [ObservableProperty]
    private string _alternate = "----";

    [ObservableProperty]
    private string _aircraftType = "----";

    [ObservableProperty]
    private string _cruiseAltitude = "---";

    [ObservableProperty]
    private string _ete = "---";

    [ObservableProperty]
    private string _routeString = "No active flight plan route";

    [ObservableProperty]
    private string _skyAnchorPhaseText = "EN-ROUTE";

    [ObservableProperty]
    private string _skyAnchorDistanceText = "---";

    [ObservableProperty]
    private bool _isSkyAnchorFrozen;

    [ObservableProperty]
    private string _exportStatusMessage = string.Empty;

    public ObservableCollection<SimBriefWaypointViewModel> Waypoints { get; } = new();

    public SimBriefPlan? CurrentPlan => _currentPlan;

    public FlightPlanViewModel(MainViewModel main, WeatherEngine weatherEngine)
    {
        _main = main;
        _weatherEngine = weatherEngine;
    }

    public void LoadFromPlan(SimBriefPlan plan)
    {
        _currentPlan = plan;
        HasPlan = true;
        FlightNumber = string.IsNullOrWhiteSpace(plan.FlightNumber) ? "SIM001" : plan.FlightNumber;
        Origin = plan.Origin;
        Destination = plan.Destination;
        Alternate = string.IsNullOrWhiteSpace(plan.Alternate) ? "NONE" : plan.Alternate;
        AircraftType = string.IsNullOrWhiteSpace(plan.AircraftType) ? "B738" : plan.AircraftType;
        CruiseAltitude = $"FL{plan.CruiseAltitudeFt / 100.0:000} ({plan.CruiseAltitudeFt:F0} ft)";
        
        var hours = (int)(plan.EstimatedTimeEnrouteMinutes / 60);
        var mins = (int)(plan.EstimatedTimeEnrouteMinutes % 60);
        Ete = $"{hours:D2}h {mins:D2}m";
        
        RouteString = string.IsNullOrWhiteSpace(plan.RouteString) ? $"{plan.Origin} DCT {plan.Destination}" : plan.RouteString;

        Waypoints.Clear();
        foreach (var wp in plan.Waypoints)
        {
            var fl = wp.AltitudeFt > 0 ? $"FL{wp.AltitudeFt / 100:000}" : CruiseAltitude;
            Waypoints.Add(new SimBriefWaypointViewModel
            {
                Identifier = wp.Identifier,
                Altitude = fl,
                Wind = $"{wp.WindDirection:000}° / {wp.WindSpeedKt:0} kt",
                Temperature = $"{wp.TemperatureC:+0;-0;0}°C",
                Stage = wp.Stage.ToString()
            });
        }

        // Prime the Sky Anchor corridor
        _weatherEngine.AnchorManager.SetFlightPlan(plan);
        StatusMessage = $"OFP active: {plan.Origin} ➔ {plan.Destination} ({plan.Waypoints.Count} waypoints)";
        UpdateAnchorTelemetry();
    }

    public void UpdateAnchorTelemetry()
    {
        var anchor = _weatherEngine.CurrentAnchorState;
        SkyAnchorPhaseText = anchor.Phase switch
        {
            SkyAnchorPhase.DepartureHold => $"DEP HOLD: {anchor.ActiveAnchorIcao ?? Origin}",
            SkyAnchorPhase.ArrivalHold => $"ARR HOLD: {anchor.ActiveAnchorIcao ?? Destination}",
            SkyAnchorPhase.FinalFreeze => "FINAL FREEZE (SHORT FINAL)",
            SkyAnchorPhase.ManualFreeze => "MANUAL HOLD FROZEN",
            _ => "CORRIDOR: EN-ROUTE"
        };

        IsSkyAnchorFrozen = anchor.IsFrozen;

        if (anchor.DistanceToDestinationNm.HasValue && anchor.DistanceToDestinationNm.Value < 300.0)
        {
            SkyAnchorDistanceText = $"{anchor.DistanceToDestinationNm.Value:F1} NM to {Destination}";
        }
        else if (anchor.DistanceToOriginNm.HasValue)
        {
            SkyAnchorDistanceText = $"{anchor.DistanceToOriginNm.Value:F1} NM from {Origin}";
        }
        else
        {
            SkyAnchorDistanceText = "En-route tracking";
        }
    }

    [RelayCommand]
    public async Task FetchPlanAsync()
    {
        if (string.IsNullOrWhiteSpace(PilotId))
        {
            StatusMessage = "Please enter a valid SimBrief Pilot ID or username.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Fetching OFP from SimBrief for {PilotId.Trim()}...";

            using var fetcher = new SimBriefFetcher();
            var plan = await fetcher.FetchPlanAsync(PilotId.Trim());

            if (plan != null && !string.IsNullOrWhiteSpace(plan.Origin) && !string.IsNullOrWhiteSpace(plan.Destination))
            {
                LoadFromPlan(plan);
                StatusMessage = $"OFP successfully loaded for {plan.FlightNumber} ({plan.Origin} ➔ {plan.Destination})";
                _main.SaveSettings();
            }
            else
            {
                StatusMessage = "No recent operational flight plan found on SimBrief. Generate a flight plan on SimBrief first.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"SimBrief fetch error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ExportPmdg()
    {
        if (_currentPlan == null)
        {
            ExportStatusMessage = "No active flight plan to export.";
            return;
        }

        try
        {
            var exportDir = GetDefaultExportDirectory();
            var savedPath = FmcWindExporter.WritePmdgWindFile(_currentPlan, exportDir);
            ExportStatusMessage = $"PMDG .wx saved: {Path.GetFileName(savedPath)}";
            
            // Also attempt to copy to clipboard for convenience
            var content = FmcWindExporter.GeneratePmdgWindFile(_currentPlan);
            try { Clipboard.SetText(content); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"PMDG export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ExportFenix()
    {
        if (_currentPlan == null)
        {
            ExportStatusMessage = "No active flight plan to export.";
            return;
        }

        try
        {
            var exportDir = GetDefaultExportDirectory();
            var json = FmcWindExporter.GenerateFenixJson(_currentPlan);
            var filePath = Path.Combine(exportDir, $"{_currentPlan.Origin}{_currentPlan.Destination}01_fenix.json");
            File.WriteAllText(filePath, json);
            ExportStatusMessage = $"Fenix JSON saved: {Path.GetFileName(filePath)}";
            try { Clipboard.SetText(json); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"Fenix export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ExportCsv()
    {
        if (_currentPlan == null)
        {
            ExportStatusMessage = "No active flight plan to export.";
            return;
        }

        try
        {
            var exportDir = GetDefaultExportDirectory();
            var csv = FmcWindExporter.GenerateCsv(_currentPlan);
            var filePath = Path.Combine(exportDir, $"{_currentPlan.Origin}{_currentPlan.Destination}_winds.csv");
            File.WriteAllText(filePath, csv);
            ExportStatusMessage = $"Winds CSV saved: {Path.GetFileName(filePath)}";
            try { Clipboard.SetText(csv); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"CSV export failed: {ex.Message}";
        }
    }

    private static string GetDefaultExportDirectory()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dir = Path.Combine(docs, "SkyWeave", "WX");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
