using System;
using System.Linq;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class AtmosphericModeler
{
    public void ApplySpatialGridAndThermals(WeatherState state, double latitude, double longitude)
    {
        // Observed surface temperature and pressure remain authoritative (NFR-A1).
        // Highest thermals around 14:00 local solar time
        var solarTimeHours = (DateTime.UtcNow.TimeOfDay.TotalHours + (longitude / 15.0)) % 24;
        if (solarTimeHours < 0) solarTimeHours += 24;

        double thermalBoost = 0;
        if (solarTimeHours >= 10 && solarTimeHours <= 18)
        {
            // Peak at 14:00 (14.0)
            var thermalCurve = Math.Sin((solarTimeHours - 10) / 8.0 * Math.PI);
            
            // Add CAPE influence if available
            double capeFactor = state.ConvectiveAvailablePotentialEnergy.HasValue 
                ? Math.Min(1.0, state.ConvectiveAvailablePotentialEnergy.Value / 2000.0) 
                : 0.2;

            thermalBoost = thermalCurve * (10 + capeFactor * 20); // 10-30 knots of thermal draft
        }

        // Apply thermal boost to the lowest wind layers (above the surface to 6000ft)
        foreach (var layer in state.WindsAloft.Where(l => !l.IsSurfaceLayer && l.AltitudeFeet > 0 && l.AltitudeFeet <= 6000))
        {
            layer.GustSpeedKnots = Math.Max(layer.GustSpeedKnots ?? 0, layer.SpeedKnots + thermalBoost);
        }
    }
}
