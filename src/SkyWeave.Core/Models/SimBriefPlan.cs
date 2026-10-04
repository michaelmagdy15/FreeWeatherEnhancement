using System;
using System.Collections.Generic;

namespace SkyWeave.Core.Models;

public enum WaypointStage
{
    Climb,
    Cruise,
    Descent
}

public class SimBriefWaypoint
{
    public string Identifier { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AltitudeFt { get; set; }
    public double WindDirection { get; set; }
    public double WindSpeedKt { get; set; }
    public double TemperatureC { get; set; }
    public WaypointStage Stage { get; set; } = WaypointStage.Cruise;
}

public class SimBriefPlan
{
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Alternate { get; set; } = string.Empty;
    public double CruiseAltitudeFt { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string AircraftType { get; set; } = string.Empty;
    public string RouteString { get; set; } = string.Empty;
    public double EstimatedTimeEnrouteMinutes { get; set; }
    public string AiracCycle { get; set; } = string.Empty;
    public string NavigraphAirac { get; set; } = string.Empty;
    public List<SimBriefWaypoint> Waypoints { get; set; } = new();
}

public class RouteIcingRisk : WeatherHazard
{
    public string WaypointIdentifier { get; set; } = string.Empty;
    public double AltitudeFt { get; set; }
    public IcingSeverity IcingSeverity { get; set; } = IcingSeverity.None;
    public IcingType IcingType { get; set; } = IcingType.Rime;
    public double TemperatureCelsius { get; set; }
}

public class RouteHazardProfile
{
    public double TotalDistanceNm { get; set; }
    public double AverageHeadwindKt { get; set; }
    public List<WeatherHazard> SeverePockets { get; set; } = new();
    public List<RouteIcingRisk> IcingRisks { get; set; } = new();
    public double MaxTurbulenceIndex { get; set; }
    public List<StormCell> IntersectingStormCells { get; set; } = new();
}
