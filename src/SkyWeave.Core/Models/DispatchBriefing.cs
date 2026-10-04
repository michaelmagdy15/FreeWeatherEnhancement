using System;
using System.Collections.Generic;

namespace SkyWeave.Core.Models;

public enum StationBriefingRole
{
    Departure,
    Destination,
    Alternate
}

public class StationWeatherBriefing
{
    public string Icao { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double ElevationFeet { get; set; }
    public StationBriefingRole Role { get; set; }

    public string RawMetar { get; set; } = string.Empty;
    public DateTime? ObservationTimeUtc { get; set; }
    public string FlightCategory { get; set; } = "VFR"; // VFR, MVFR, IFR, LIFR
    public string SurfaceWindText { get; set; } = string.Empty;
    public string VisibilityText { get; set; } = string.Empty;
    public string CeilingText { get; set; } = string.Empty;
    public double TemperatureCelsius { get; set; }
    public double DewpointCelsius { get; set; }
    public double AltimeterHpa { get; set; }
    public double AltimeterInHg { get; set; }

    public string RawTaf { get; set; } = string.Empty;
    public string TafSummary { get; set; } = string.Empty;
    public string RunwayWindAnalysis { get; set; } = string.Empty;
}

public class NavlogWaypointEntry
{
    public string Identifier { get; set; } = string.Empty;
    public WaypointStage Stage { get; set; } = WaypointStage.Cruise;
    public double AltitudeFt { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double WindDirection { get; set; }
    public double WindSpeedKt { get; set; }
    public double TemperatureCelsius { get; set; }
    public double HeadwindComponentKt { get; set; } // Positive = headwind, Negative = tailwind
    public double CrosswindComponentKt { get; set; } // Absolute crosswind
    public string IcingRisk { get; set; } = "None";
    public string TurbulenceRisk { get; set; } = "Smooth";
}

public class DispatchHazardSummary
{
    public double TotalDistanceNm { get; set; }
    public double AverageHeadwindKt { get; set; }
    public double MaxTurbulenceIndex { get; set; }
    public string MaxTurbulenceDescription { get; set; } = "Smooth";
    public int IcingRiskCount { get; set; }
    public int IntersectingStormCellsCount { get; set; }
    public int SigmetCount { get; set; }
    public List<string> CriticalAdvisories { get; set; } = new();
    public List<string> DispatcherNotes { get; set; } = new();
}

public class DispatchBriefing
{
    public string FlightNumber { get; set; } = string.Empty;
    public string AircraftType { get; set; } = string.Empty;
    public string OriginIcao { get; set; } = string.Empty;
    public string DestinationIcao { get; set; } = string.Empty;
    public string? AlternateIcao { get; set; }
    public double CruiseAltitudeFt { get; set; }
    public double EstimatedTimeEnrouteMinutes { get; set; }
    public string RouteString { get; set; } = string.Empty;
    public string AiracCycle { get; set; } = string.Empty;
    public string ReleaseNumber { get; set; } = "REL-01";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public StationWeatherBriefing? DepartureBriefing { get; set; }
    public StationWeatherBriefing? DestinationBriefing { get; set; }
    public StationWeatherBriefing? AlternateBriefing { get; set; }

    public List<NavlogWaypointEntry> Navlog { get; set; } = new();
    public DispatchHazardSummary HazardSummary { get; set; } = new();
}
