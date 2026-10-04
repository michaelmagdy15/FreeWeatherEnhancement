using System;
using System.Collections.Generic;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

/// <summary>
/// Aeronautical chart service providing free worldwide AIP charts, airport diagrams,
/// approach plates, and visual charts via Airmate (https://fly.airmate.aero), ChartFox,
/// and FAA digital procedures as a free alternative to paid subscriptions like Navigraph.
/// Also provides deep links to the official MSFS 2024 Web Flight Planner.
/// </summary>
public class AirmateChartService
{
    public const string AirmatePortalUrl = "https://www.airmate.aero/";
    public const string AirmateAppBaseUrl = "https://fly.airmate.aero/#/aerodrome/";
    public const string ChartFoxBaseUrl = "https://chartfox.org/";
    public const string MsfsPlannerUrl = "https://planner.flightsimulator.com/";
    public const string FaaDtppSearchBaseUrl = "https://www.faa.gov/air_traffic/flight_info/aeronav/digital_products/dtpp/search/results/?cycle=current&ident=";
    public const string NavigraphBaseUrl = "https://charts.navigraph.com/airport/";

    /// <summary>
    /// Returns the direct aerodrome page in Airmate for an airport ICAO.
    /// </summary>
    public static string GetAirmateAirportUrl(string icao)
    {
        if (string.IsNullOrWhiteSpace(icao)) return AirmatePortalUrl;
        return $"{AirmateAppBaseUrl}{icao.Trim().ToUpperInvariant()}";
    }

    /// <summary>
    /// Returns the direct aerodrome page in ChartFox (free VATSIM / open AIP).
    /// </summary>
    public static string GetChartFoxAirportUrl(string icao)
    {
        if (string.IsNullOrWhiteSpace(icao)) return ChartFoxBaseUrl;
        return $"{ChartFoxBaseUrl}{icao.Trim().ToUpperInvariant()}";
    }

    /// <summary>
    /// Returns the official FAA Digital Terminal Procedures (d-TPP) search URL for US airports.
    /// </summary>
    public static string? GetFaaDtppUrl(string icao)
    {
        if (string.IsNullOrWhiteSpace(icao)) return null;
        var clean = icao.Trim().ToUpperInvariant();
        if (clean.StartsWith("K") || clean.StartsWith("P") || clean.StartsWith("T"))
        {
            return $"{FaaDtppSearchBaseUrl}{clean}";
        }
        return null;
    }

    /// <summary>
    /// Returns the primary chart URL for an airport based on user's active provider selection.
    /// </summary>
    public static string GetPrimaryChartUrl(string icao, string provider = "Airmate")
    {
        if (string.IsNullOrWhiteSpace(icao))
        {
            return provider.Equals("Navigraph", StringComparison.OrdinalIgnoreCase)
                ? "https://charts.navigraph.com/"
                : AirmatePortalUrl;
        }

        var clean = icao.Trim().ToUpperInvariant();
        if (provider.Equals("Navigraph", StringComparison.OrdinalIgnoreCase))
        {
            return $"{NavigraphBaseUrl}{clean}";
        }
        if (provider.Equals("ChartFox", StringComparison.OrdinalIgnoreCase))
        {
            return GetChartFoxAirportUrl(clean);
        }

        // Default to Airmate (Free AIP)
        return GetAirmateAirportUrl(clean);
    }

    /// <summary>
    /// Returns the official MSFS 2024 Web Flight Planner URL.
    /// </summary>
    public static string GetMsfsPlannerUrl() => MsfsPlannerUrl;

    /// <summary>
    /// Resolves standard chart references (Diagram, Approaches, SIDs, STARs, VAC) for an airport.
    /// </summary>
    public static IReadOnlyList<AviationChart> GetStandardAirportCharts(string icao, string provider = "Airmate")
    {
        if (string.IsNullOrWhiteSpace(icao)) return Array.Empty<AviationChart>();

        var clean = icao.Trim().ToUpperInvariant();
        var list = new List<AviationChart>();

        // 1. Aerodrome & Ground Diagram
        var faaUrl = GetFaaDtppUrl(clean);
        if (faaUrl != null)
        {
            list.Add(new AviationChart(clean, $"{clean} FAA Digital Terminal Procedures (d-TPP)", ChartCategory.AirportDiagram, faaUrl, "FAA d-TPP (Free Official)", true));
        }

        list.Add(new AviationChart(clean, $"{clean} Aerodrome & AIP Charts", ChartCategory.AirportDiagram, GetAirmateAirportUrl(clean), "Airmate (Free AIP)", true));

        // 2. Open AIP / ChartFox alternative
        list.Add(new AviationChart(clean, $"{clean} Open Charts & Procedures", ChartCategory.Approach, GetChartFoxAirportUrl(clean), "ChartFox (Free AIP)", true));

        // 3. IFR Approaches & Procedures via Airmate
        list.Add(new AviationChart(clean, $"{clean} IFR Approach Plates & Minimums", ChartCategory.Approach, GetAirmateAirportUrl(clean), "Airmate (Free AIP)", true));

        // 4. SIDs & STARs Departure/Arrival via Airmate
        list.Add(new AviationChart(clean, $"{clean} Standard Instrument Departures (SIDs)", ChartCategory.Departure, GetAirmateAirportUrl(clean), "Airmate (Free AIP)", true));
        list.Add(new AviationChart(clean, $"{clean} Standard Terminal Arrival Routes (STARs)", ChartCategory.Arrival, GetAirmateAirportUrl(clean), "Airmate (Free AIP)", true));

        // 5. Visual Approach Charts (VAC)
        list.Add(new AviationChart(clean, $"{clean} Visual Approach Chart (VAC / VFR)", ChartCategory.VisualApproach, GetAirmateAirportUrl(clean), "Airmate (Free AIP)", true));

        // If user also has Navigraph configured
        if (provider.Equals("Navigraph", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new AviationChart(clean, $"{clean} Navigraph Jeppesen Charts", ChartCategory.General, $"{NavigraphBaseUrl}{clean}", "Navigraph (Subscription)", false));
        }

        return list;
    }

    /// <summary>
    /// Validates user credentials format for Airmate account.
    /// </summary>
    public static (bool isValid, string message) ValidateAccountDetails(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return (false, "Please enter your Airmate username or email address.");
        }

        var trimmedUser = username.Trim();
        if (trimmedUser.Length < 3)
        {
            return (false, "Airmate username or email must be at least 3 characters.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Please enter your Airmate password.");
        }

        return (true, $"Airmate account '{trimmedUser}' saved. Free official AIP charts active!");
    }
}
