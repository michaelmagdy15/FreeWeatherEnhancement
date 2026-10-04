using System.Globalization;
using System.Text;
using System.Text.Json;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

/// <summary>
/// Generates FMC / FMGS route winds aloft uplink files for major flight simulator airliners,
/// including PMDG 737/777 (.wx format), Fenix A320, and generic navigation formats.
/// </summary>
public static class FmcWindExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Generates PMDG .wx format text for FMC wind uplink (e.g., EGLLKJFK01.wx).
    /// </summary>
    public static string GeneratePmdgWindFile(SimBriefPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));

        var sb = new StringBuilder();
        var origin = string.IsNullOrWhiteSpace(plan.Origin) ? "ZZZZ" : plan.Origin.Trim().ToUpperInvariant();
        var dest = string.IsNullOrWhiteSpace(plan.Destination) ? "ZZZZ" : plan.Destination.Trim().ToUpperInvariant();
        var flightId = $"{origin}{dest}01";

        sb.AppendLine("WXR");
        sb.AppendLine(flightId);

        // Waypoints section
        foreach (var wp in plan.Waypoints)
        {
            if (string.IsNullOrWhiteSpace(wp.Identifier)) continue;

            // Compute Flight Level or Altitude
            var altFt = wp.AltitudeFt > 0 ? wp.AltitudeFt : plan.CruiseAltitudeFt;
            var fl = Math.Round(altFt / 100.0);
            var dir = ((int)Math.Round(wp.WindDirection) % 360);
            if (dir < 0) dir += 360;
            var spd = (int)Math.Round(wp.WindSpeedKt);
            var temp = (int)Math.Round(wp.TemperatureC);

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-7} {1,3:D3} {2,3:D3}/{3,3:D3} {4:+00;-00;+00}",
                wp.Identifier.ToUpperInvariant(),
                (int)fl,
                dir,
                spd,
                temp));
        }

        // Descent forecast winds section
        var descentWps = plan.Waypoints
            .Where(w => w.Stage == WaypointStage.Descent && w.AltitudeFt > 0)
            .OrderByDescending(w => w.AltitudeFt)
            .ToList();

        if (descentWps.Count > 0)
        {
            sb.AppendLine("DESCENT");
            foreach (var dwp in descentWps.Take(4))
            {
                var fl = (int)Math.Round(dwp.AltitudeFt / 100.0);
                var dir = ((int)Math.Round(dwp.WindDirection) % 360);
                if (dir < 0) dir += 360;
                var spd = (int)Math.Round(dwp.WindSpeedKt);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "FL{0,3:D3} {1,3:D3}/{2,3:D3}",
                    fl,
                    dir,
                    spd));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes the PMDG .wx wind file to the specified target directory.
    /// Returns the full file path if successful.
    /// </summary>
    public static string WritePmdgWindFile(SimBriefPlan plan, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentNullException(nameof(targetDirectory));

        Directory.CreateDirectory(targetDirectory);
        var origin = string.IsNullOrWhiteSpace(plan.Origin) ? "ZZZZ" : plan.Origin.Trim().ToUpperInvariant();
        var dest = string.IsNullOrWhiteSpace(plan.Destination) ? "ZZZZ" : plan.Destination.Trim().ToUpperInvariant();
        var fileName = $"{origin}{dest}01.wx";
        var fullPath = Path.Combine(targetDirectory, fileName);

        var content = GeneratePmdgWindFile(plan);
        File.WriteAllText(fullPath, content, Encoding.ASCII);
        return fullPath;
    }

    /// <summary>
    /// Generates structured JSON wind payload suitable for Fenix A320 AOC or external EFB uplink.
    /// </summary>
    public static string GenerateFenixJson(SimBriefPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));

        var payload = new
        {
            origin = plan.Origin,
            destination = plan.Destination,
            alternate = plan.Alternate,
            cruiseAltitudeFt = plan.CruiseAltitudeFt,
            flightNumber = plan.FlightNumber,
            aircraftType = plan.AircraftType,
            waypoints = plan.Waypoints.Select(w => new
            {
                ident = w.Identifier,
                lat = w.Latitude,
                lon = w.Longitude,
                altFt = w.AltitudeFt > 0 ? w.AltitudeFt : plan.CruiseAltitudeFt,
                windDir = Math.Round(w.WindDirection),
                windSpdKt = Math.Round(w.WindSpeedKt),
                tempC = Math.Round(w.TemperatureC),
                stage = w.Stage.ToString()
            }).ToList()
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>
    /// Generates CSV format route winds aloft data for spreadsheet analysis or custom flight planners.
    /// </summary>
    public static string GenerateCsv(SimBriefPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));

        var sb = new StringBuilder();
        sb.AppendLine("Identifier,Latitude,Longitude,AltitudeFt,WindDirectionDeg,WindSpeedKt,TemperatureC,Stage");

        foreach (var wp in plan.Waypoints)
        {
            var altFt = wp.AltitudeFt > 0 ? wp.AltitudeFt : plan.CruiseAltitudeFt;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1:F4},{2:F4},{3:F0},{4:F0},{5:F0},{6:F1},{7}",
                wp.Identifier,
                wp.Latitude,
                wp.Longitude,
                altFt,
                wp.WindDirection,
                wp.WindSpeedKt,
                wp.TemperatureC,
                wp.Stage));
        }

        return sb.ToString();
    }
}
