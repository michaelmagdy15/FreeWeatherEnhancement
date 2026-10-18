using System;
using System.Collections.Generic;

namespace SkyWeave.Core.Models;

public enum RunwaySuitability
{
    Recommended,
    Favorable,
    Caution,
    Unfavorable,
    LimitExceeded
}

public class RunwayInfo
{
    public string Identifier { get; set; } = string.Empty;
    public double HeadingDegrees { get; set; }
    public int LengthFeet { get; set; }
    public string Surface { get; set; } = "Hard";
}

public class RunwayAnalysis
{
    public string Runway { get; set; } = string.Empty;
    public double HeadingDegrees { get; set; }
    public double WindAngleDeltaDegrees { get; set; }
    public double HeadwindKnots { get; set; }
    public double CrosswindKnots { get; set; }
    public string CrosswindSide { get; set; } = "Calm";
    public double GustHeadwindKnots { get; set; }
    public double GustCrosswindKnots { get; set; }
    public RunwaySuitability Suitability { get; set; } = RunwaySuitability.Favorable;
    public string SuitabilityText { get; set; } = "Favorable";
    public string Summary { get; set; } = string.Empty;
}

public class RunwayWindReport
{
    public string StationId { get; set; } = string.Empty;
    public double WindDirectionDegrees { get; set; }
    public double WindSpeedKnots { get; set; }
    public double? WindGustKnots { get; set; }
    public string? RecommendedRunway { get; set; }
    public RunwayAnalysis? BestRunway { get; set; }
    public List<RunwayAnalysis> Runways { get; set; } = new();
    public string AdvisoryMessage { get; set; } = string.Empty;
}
