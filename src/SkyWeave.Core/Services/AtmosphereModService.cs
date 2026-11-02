using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

/// <summary>
/// Atmospheric optics, Fortuin &amp; Kelder (1998) ozone climatology, boundary-layer aerosol extinction,
/// 32-slice cloud anchoring, and CommBus/tunables.ini bridge for AtmosphereMod integration.
/// </summary>
public static class AtmosphereModCalculator
{
    //MSFS stock reference extinction at sea level: 1.594e-6 m^-1 (exp(-z / 8000 m))
    public const double SigmaRefPerMeter = 1.594e-6;
    private const double FeetToMeters = 0.3048;

    /// <summary>
    /// Fortuin &amp; Kelder (1998) monthly-by-latitude-band zonal mean total column ozone in Dobson Units.
    /// 12 months (Jan=0 .. Dec=11) x 17 latitude bands (-80..+80 deg in 10-deg steps).
    /// J. Geophys. Res., 103(D24), 31709-31734.
    /// </summary>
    private static readonly short[,] OzoneDobsonTable = new short[12, 17]
    {
        // -80  -70  -60  -50  -40  -30  -20  -10    0  +10  +20  +30  +40  +50  +60  +70  +80
        { 303, 314, 333, 319, 294, 273, 259, 254, 250, 245, 252, 279, 323, 358, 369, 369, 369 }, // Jan
        { 294, 303, 316, 304, 284, 268, 258, 254, 251, 249, 260, 292, 344, 386, 405, 417, 427 }, // Feb
        { 288, 296, 305, 296, 279, 265, 258, 256, 255, 256, 270, 301, 353, 396, 418, 437, 454 }, // Mar
        { 289, 293, 302, 298, 282, 268, 259, 258, 258, 262, 278, 305, 349, 388, 408, 424, 437 }, // Apr
        { 294, 300, 309, 306, 290, 272, 260, 257, 257, 264, 279, 302, 337, 370, 385, 393, 398 }, // May
        { 293, 303, 319, 319, 303, 280, 262, 256, 257, 264, 276, 292, 319, 346, 355, 355, 352 }, // Jun
        { 281, 296, 324, 331, 315, 289, 266, 258, 258, 264, 272, 282, 301, 324, 330, 324, 316 }, // Jul
        { 251, 274, 322, 343, 328, 298, 271, 261, 261, 265, 270, 276, 290, 308, 311, 302, 292 }, // Aug
        { 204, 237, 313, 356, 341, 307, 277, 265, 264, 265, 267, 271, 282, 298, 300, 290, 280 }, // Sep
        { 200, 239, 323, 360, 339, 305, 277, 265, 263, 261, 261, 266, 280, 299, 303, 295, 288 }, // Oct
        { 256, 288, 346, 349, 322, 292, 269, 260, 257, 252, 253, 264, 288, 316, 325, 321, 319 }, // Nov
        { 309, 325, 349, 334, 306, 280, 262, 256, 252, 246, 250, 270, 306, 340, 351, 349, 349 }  // Dec
    };

    /// <summary>
    /// Computes zonal-mean total column ozone in Dobson Units for the given latitude (-90..+90 deg)
    /// and day of year (1..366) via bilinear interpolation across Fortuin &amp; Kelder (1998).
    /// </summary>
    public static double ComputeOzoneDobsonUnits(double latitudeDegrees, int dayOfYear)
    {
        double lat = Math.Clamp(latitudeDegrees, -80.0, 80.0);
        double bandF = (lat + 80.0) * 0.1;
        int b0 = (int)Math.Floor(bandF);
        if (b0 < 0) b0 = 0;
        if (b0 > 15) b0 = 15;
        int b1 = b0 + 1;
        double tb = bandF - b0;

        // Mid-month day-of-year anchors (~day 15 of each month; 30.4375 days/month)
        double mF = (dayOfYear - 15.2) / 30.4375;
        int m0 = ((int)Math.Floor(mF) % 12 + 12) % 12;
        int m1 = (m0 + 1) % 12;
        double tm = mF - Math.Floor(mF);

        double v0 = OzoneDobsonTable[m0, b0] * (1.0 - tb) + OzoneDobsonTable[m0, b1] * tb;
        double v1 = OzoneDobsonTable[m1, b0] * (1.0 - tb) + OzoneDobsonTable[m1, b1] * tb;
        return v0 * (1.0 - tm) + v1 * tm;
    }

    /// <summary>
    /// Computes relative humidity [0..1] from temperature and dewpoint (°C) via the Magnus formula.
    /// </summary>
    public static double ComputeRelativeHumidity01(double temperatureC, double dewpointC)
    {
        const double a = 17.625;
        const double b = 243.04;
        double gammaTd = (a * dewpointC) / (b + dewpointC);
        double gammaT = (a * temperatureC) / (b + temperatureC);
        double rh = Math.Exp(gammaTd - gammaT);
        return Math.Clamp(rh, 0.0, 1.0);
    }

    /// <summary>
    /// Classifies the dominant aerosol optical regime from METAR weather phenomena tokens and raw METAR text.
    /// Priority order matches haze_mapping.h: VolcanicAsh &gt; Dust &gt; Smoke &gt; Haze &gt; Continental.
    /// </summary>
    public static AtmosphereAirMass ClassifyAirMass(
        IReadOnlyList<string>? weatherConditions,
        string? rawMetar,
        out bool hasPrecipitation,
        out bool hasMistOrFog)
    {
        hasPrecipitation = false;
        hasMistOrFog = false;

        var tokens = new List<string>();
        if (weatherConditions != null)
        {
            foreach (var cond in weatherConditions)
            {
                if (!string.IsNullOrWhiteSpace(cond))
                    tokens.Add(cond.ToUpperInvariant());
            }
        }

        if (!string.IsNullOrWhiteSpace(rawMetar))
        {
            // Extract weather codes from raw METAR body (before RMK)
            var body = rawMetar.ToUpperInvariant();
            int rmkIdx = body.IndexOf(" RMK", StringComparison.Ordinal);
            if (rmkIdx > 0)
                body = body[..rmkIdx];

            foreach (Match m in Regex.Matches(body, @"(?:^|\s)(?:\+|-|VC)?(DZ|RA|SN|SG|IC|PL|GR|GS|UP|BR|FG|FU|VA|DU|SA|HZ|PY|PO|SQ|FC|SS|DS|TS|SH|FZ|MI|PR|BC|DR|BL)+(?=\s|$)"))
            {
                tokens.Add(m.Value.Trim());
            }
        }

        AtmosphereAirMass best = AtmosphereAirMass.Continental;

        foreach (var token in tokens)
        {
            if (Regex.IsMatch(token, @"(DZ|RA|SN|SG|IC|PL|GR|GS|UP|SH)"))
                hasPrecipitation = true;

            if (token.Contains("FG", StringComparison.Ordinal) || token.Contains("BR", StringComparison.Ordinal))
                hasMistOrFog = true;

            if (token.Contains("VA", StringComparison.Ordinal))
            {
                best = MaxAirMass(best, AtmosphereAirMass.VolcanicAsh);
            }
            else if (token.Contains("DU", StringComparison.Ordinal) ||
                     token.Contains("SA", StringComparison.Ordinal) ||
                     token.Contains("DS", StringComparison.Ordinal) ||
                     token.Contains("SS", StringComparison.Ordinal) ||
                     token.Contains("PO", StringComparison.Ordinal))
            {
                best = MaxAirMass(best, AtmosphereAirMass.Dust);
            }
            else if (token.Contains("FU", StringComparison.Ordinal))
            {
                best = MaxAirMass(best, AtmosphereAirMass.Smoke);
            }
            else if (token.Contains("HZ", StringComparison.Ordinal) ||
                     token.Contains("BR", StringComparison.Ordinal) ||
                     token.Contains("FG", StringComparison.Ordinal) ||
                     token.Contains("PY", StringComparison.Ordinal))
            {
                best = MaxAirMass(best, AtmosphereAirMass.Haze);
            }
        }

        return best;
    }

    private static AtmosphereAirMass MaxAirMass(AtmosphereAirMass a, AtmosphereAirMass b)
        => (int)b > (int)a ? b : a;

    /// <summary>
    /// Derives the complete AtmosphereModState (T1..T6, T9, T10, T14, and 32-slice cloud anchor)
    /// from a live MetarData observation, station elevation, coordinates, and UTC time.
    /// Exact C# port of haze::derive() + ozone::dobson_units() + cloud_anchor::build_anchor().
    /// </summary>
    public static AtmosphereModState ComputeState(
        MetarData metar,
        double latitudeDegrees,
        double longitudeDegrees,
        double stationElevationMeters,
        DateTime utcNow,
        AtmosphereModTunables? tunables = null)
    {
        ArgumentNullException.ThrowIfNull(metar);

        double localSolarHour = (utcNow.TimeOfDay.TotalHours + (longitudeDegrees / 15.0)) % 24.0;
        if (localSolarHour < 0) localSolarHour += 24.0;

        double groundElevationKm = Math.Max(0.0, stationElevationMeters * 0.001);
        double rh = ComputeRelativeHumidity01(metar.TemperatureCelsius, metar.DewpointCelsius);
        var airMass = ClassifyAirMass(metar.WeatherConditions, metar.RawText, out bool hasPrecip, out bool hasMistOrFog);

        bool cavok = metar.RawText.Contains("CAVOK", StringComparison.OrdinalIgnoreCase) ||
                     (metar.VisibilityMeters >= 9999.0 && metar.Clouds.Count == 0 && metar.WeatherConditions.Count == 0);

        double visMeters = metar.VisibilityMeters > 0 ? metar.VisibilityMeters : 10000.0;

        // 1. Visibility -> baseline aerosol extinction multiplier T3
        double t3FromVis = 0.0;
        bool visibilityRestricted = !cavok && visMeters > 0.0 && visMeters < 9999.0;

        if (visibilityRestricted)
        {
            double v = Math.Max(visMeters, 200.0);
            if (!hasMistOrFog)
            {
                v = Math.Max(v, 3500.0);
            }

            // Koschmieder's law with effective contrast threshold C_eff = 0.22 (ln(1/0.22) = 1.5)
            // accounting for MSFS volumetric cloud-pass fog that already supplies ~60% of low-vis reduction.
            const double kContrast = 1.5;
            const double rayleighSea = 1.16e-5; // m^-1 at 550 nm
            double sigmaTarget = Math.Max(kContrast / v - rayleighSea, 0.0);

            double sigmaRefGround = SigmaRefPerMeter * Math.Exp(-groundElevationKm / 8.0);
            double mult = sigmaTarget / Math.Max(sigmaRefGround, 1e-8);

            // Soft knee above T3 = 30 up to asymptotic cap 80
            const double knee = 30.0;
            const double cap = 80.0;
            if (mult > knee)
            {
                double excess = mult - knee;
                double span = cap - knee;
                mult = knee + span * (excess / (excess + span));
            }
            t3FromVis = mult;
        }

        // 2. Relative humidity -> hygroscopic aerosol growth f(RH) = (1 - min(RH, 0.95))^-0.55
        double rhClamped = Math.Min(rh, 0.95);
        double fRh = Math.Pow(1.0 - rhClamped, -0.55);

        // Temperature factor: warmer air holds higher biogenic/secondary organic aerosol loading
        double tempFactor = Math.Clamp(0.6 + 0.025 * metar.TemperatureCelsius, 0.4, 1.6);

        const double dryBase = 1.0;
        double t3FromRh = dryBase * tempFactor * fRh;

        // Dry haze inference: restricted visibility (< 9000 m), dry air (RH < 65%), no precip, no phenomenon code
        bool inferredDryHaze = false;
        if (visibilityRestricted && visMeters < 9000.0 && rh < 0.65 && !hasPrecip && airMass == AtmosphereAirMass.Continental)
        {
            inferredDryHaze = true;
            airMass = AtmosphereAirMass.Haze;
        }

        double t3 = Math.Max(t3FromVis, t3FromRh);
        if (hasPrecip && !visibilityRestricted)
        {
            // Wet scavenging (rain washout) when visibility is unrestricted
            t3 *= 0.6;
        }
        t3 = Math.Clamp(t3, 0.5, 80.0);

        // 3. Free troposphere haze T4 and scale height T5
        double t4 = Math.Clamp(0.35 + 0.04 * Math.Min(t3, 15.0), 0.25, 1.2);
        double t5 = 2.5;
        if (airMass == AtmosphereAirMass.Smoke || airMass == AtmosphereAirMass.Dust || airMass == AtmosphereAirMass.VolcanicAsh)
        {
            // Elevated smoke/dust plumes reach higher into the free troposphere
            t4 = Math.Clamp(t4 * 1.6, 0.4, 2.0);
            t5 = 3.5;
        }

        // 4. Boundary-layer top altitude T1 (km MSL) and transition edge width T2 (km)
        double lowestCloudAglKm = 100.0;
        bool hasLowOvercast = false;
        foreach (var cl in metar.Clouds)
        {
            if (cl.BaseFeet > 0)
            {
                double baseKm = cl.BaseFeet * FeetToMeters * 0.001;
                if (baseKm < lowestCloudAglKm)
                    lowestCloudAglKm = baseKm;

                string cov = cl.Coverage.ToUpperInvariant();
                if ((cov is "BKN" or "OVC" or "VV") && baseKm < 1.2)
                    hasLowOvercast = true;
            }
        }

        double diurnal = 0.0;
        if (localSolarHour >= 6.0 && localSolarHour <= 20.0)
        {
            double s = Math.Sin((localSolarHour - 6.0) / 14.0 * Math.PI);
            diurnal = Math.Max(0.0, s);
        }

        double blDepthKm = 0.6
            + 0.7 * diurnal * Math.Clamp((metar.TemperatureCelsius - 5.0) / 25.0, 0.2, 1.3)
            + 0.15 * (1.0 - rh);

        if (hasLowOvercast && lowestCloudAglKm < blDepthKm)
        {
            blDepthKm = Math.Max(0.25, lowestCloudAglKm + 0.15);
        }
        if (hasMistOrFog && visMeters < 2000.0)
        {
            // Radiation fog / shallow mist trapped in a surface inversion
            blDepthKm = Math.Min(blDepthKm, 0.35);
        }
        blDepthKm = Math.Clamp(blDepthKm, 0.25, 2.8);

        double t1 = groundElevationKm + blDepthKm;
        double t2 = Math.Clamp(blDepthKm * 0.15, 0.08, 0.35);

        // 5. Aerosol optical properties: Brown carbon absorption T6, DHG asymmetry T9, Ångström exponent T10
        double t6 = 0.05;
        double t9 = 0.70;
        double t10 = 1.30;

        switch (airMass)
        {
            case AtmosphereAirMass.Continental:
            {
                double wet = SmoothStep(0.55, 0.95, rh);
                t10 = Lerp(1.45, 0.55, wet);
                t9 = Lerp(0.67, 0.76, wet);
                t6 = 0.04;
                break;
            }
            case AtmosphereAirMass.Haze:
            {
                double wet = SmoothStep(0.50, 0.95, rh);
                t10 = inferredDryHaze ? 1.40 : Lerp(1.20, 0.35, wet);
                t9 = inferredDryHaze ? 0.68 : Lerp(0.70, 0.78, wet);
                t6 = inferredDryHaze ? 0.10 : 0.06;
                break;
            }
            case AtmosphereAirMass.Smoke:
            {
                t10 = 1.75;
                t9 = 0.64;
                t6 = 0.25;
                break;
            }
            case AtmosphereAirMass.Dust:
            {
                t10 = 0.25;
                t9 = 0.76;
                t6 = 0.30;
                break;
            }
            case AtmosphereAirMass.VolcanicAsh:
            {
                t10 = 0.15;
                t9 = 0.72;
                t6 = 0.20;
                break;
            }
        }

        // 6. Ozone column T14 (Fortuin & Kelder 1998 climatology unless user fixed T14 > 0)
        double manualOzone = tunables != null && tunables.T[14] > 0.0 ? tunables.T[14] : 0.0;
        double ozoneDu = manualOzone > 0.0
            ? manualOzone
            : ComputeOzoneDobsonUnits(latitudeDegrees, utcNow.DayOfYear);

        // 7. Build 32-slice cloud anchor profile & storm tower evidence
        var slices = BuildCloudAnchorSlices(metar, stationElevationMeters, rh, hasMistOrFog, out double stormStrength);

        string stationLabel = string.IsNullOrWhiteSpace(metar.StationId) ? "AUTO" : metar.StationId.ToUpperInvariant();
        string visLabel = cavok ? "CAVOK" : $"{visMeters:F0}m";
        string statusSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{stationLabel}: {metar.TemperatureCelsius:F0}/{metar.DewpointCelsius:F0}°C (RH {rh * 100.0:F0}%) vis {visLabel} [{airMass}] -> T1={t1:F2}km T3={t3:F2} T4={t4:F2} T6={t6:F2} T9={t9:F2} T10={t10:F2} O3={ozoneDu:F0}DU");

        return new AtmosphereModState
        {
            AirMass = airMass,
            RelativeHumidity01 = rh,
            BoundaryLayerTopKmMsl = t1,
            BoundaryLayerEdgeWidthKm = t2,
            BoundaryLayerHazeMultiplier = t3,
            FreeTroposphereHazeMultiplier = t4,
            FreeTroposphereScaleHeightKm = t5,
            BrownCarbonAbsorption = t6,
            AerosolAsymmetryG = t9,
            AngstromExponent = t10,
            OzoneDobsonUnits = ozoneDu,
            InferredDryHaze = inferredDryHaze,
            StormTowerStrength = stormStrength,
            StatusSummary = statusSummary,
            CloudAnchorSlices = slices
        };
    }

    /// <summary>
    /// Builds the 32 vertical weather-map altitude slices (z_m = 15.625 * k^2, k = 0..31)
    /// used by AtmosphereMod's cloud anchor and convective tower shaper (cloud_anchor.h).
    /// </summary>
    public static List<AtmosphereCloudSlice> BuildCloudAnchorSlices(
        MetarData metar,
        double stationElevationMeters,
        double relativeHumidity01,
        bool hasMistOrFog,
        out double stormTowerStrength)
    {
        stormTowerStrength = 0.0;
        var slices = new List<AtmosphereCloudSlice>(32);
        for (int k = 0; k < 32; k++)
        {
            slices.Add(new AtmosphereCloudSlice
            {
                Index = k,
                AltitudeMetersMsl = 15.625 * k * k,
                Coverage = 0.0,
                Density = 0.0,
                Kind = AtmosphereCloudKind.Cumuliform
            });
        }

        bool hasThunderstormToken = metar.WeatherConditions.Any(w =>
            w.Contains("TS", StringComparison.OrdinalIgnoreCase) ||
            w.Contains("SQ", StringComparison.OrdinalIgnoreCase) ||
            w.Contains("FC", StringComparison.OrdinalIgnoreCase)) ||
            Regex.IsMatch(metar.RawText ?? string.Empty, @"\b(\+|-|VC)?TS", RegexOptions.IgnoreCase);

        bool hasHeavyShowers = metar.WeatherConditions.Any(w =>
            w.Contains("+SH", StringComparison.OrdinalIgnoreCase) ||
            w.Contains("GR", StringComparison.OrdinalIgnoreCase) ||
            w.Contains("GS", StringComparison.OrdinalIgnoreCase));

        bool hasCbLayer = metar.Clouds.Any(c =>
            string.Equals(c.Type, "CB", StringComparison.OrdinalIgnoreCase));
        bool hasTcuLayer = metar.Clouds.Any(c =>
            string.Equals(c.Type, "TCU", StringComparison.OrdinalIgnoreCase));

        if (hasThunderstormToken || hasCbLayer)
            stormTowerStrength = hasHeavyShowers ? 1.0 : 0.85;
        else if (hasTcuLayer || hasHeavyShowers)
            stormTowerStrength = 0.55;

        if (metar.Clouds.Count == 0)
            return slices;

        // Tropopause ceiling estimate for CB anvils (~11.5 km mid-latitudes)
        const double cbAnvilTopMsl = 11500.0;

        foreach (var layer in metar.Clouds)
        {
            string covCode = (layer.Coverage ?? string.Empty).ToUpperInvariant();
            double cov01 = covCode switch
            {
                "FEW" => 2.0 / 8.0,
                "SCT" => 4.0 / 8.0,
                "BKN" => 6.0 / 8.0,
                "OVC" or "VV" => 8.0 / 8.0,
                _ => 0.0
            };
            if (cov01 <= 0.0) continue;

            double baseAglM = Math.Max(0.0, layer.BaseFeet * FeetToMeters);
            double baseMslM = stationElevationMeters + baseAglM;

            bool isCb = string.Equals(layer.Type, "CB", StringComparison.OrdinalIgnoreCase) ||
                        (hasThunderstormToken && cov01 >= 0.5);
            bool isTcu = string.Equals(layer.Type, "TCU", StringComparison.OrdinalIgnoreCase);

            AtmosphereCloudKind kind;
            double thicknessM;
            double density;

            if (isCb)
            {
                kind = AtmosphereCloudKind.Convective;
                thicknessM = Math.Max(4500.0, cbAnvilTopMsl - baseMslM);
                density = 0.95;
            }
            else if (isTcu)
            {
                kind = AtmosphereCloudKind.Convective;
                thicknessM = 3500.0;
                density = 0.85;
            }
            else if ((hasMistOrFog || relativeHumidity01 > 0.92) && baseAglM < 450.0 && cov01 >= 0.75)
            {
                kind = AtmosphereCloudKind.SmoothDeck;
                thicknessM = 350.0;
                density = 0.75;
            }
            else if (cov01 >= 0.75 && baseAglM < 2000.0)
            {
                kind = AtmosphereCloudKind.Stratiform;
                thicknessM = 550.0 + 350.0 * cov01;
                density = 0.70;
            }
            else
            {
                kind = cov01 >= 0.75 ? AtmosphereCloudKind.Stratiform : AtmosphereCloudKind.Cumuliform;
                thicknessM = 450.0 + 600.0 * cov01;
                density = 0.55 + 0.25 * cov01;
            }

            double topMslM = baseMslM + thicknessM;
            double softMarginM = Math.Max(120.0, thicknessM * 0.20);

            for (int k = 0; k < 32; k++)
            {
                var slice = slices[k];
                double z = slice.AltitudeMetersMsl;
                if (z < baseMslM - softMarginM || z > topMslM + softMarginM)
                    continue;

                double weight = 1.0;
                if (z < baseMslM)
                    weight = 1.0 - (baseMslM - z) / softMarginM;
                else if (z > topMslM)
                    weight = 1.0 - (z - topMslM) / softMarginM;

                weight = Math.Clamp(weight, 0.0, 1.0);
                double sliceCov = cov01 * weight;
                if (sliceCov > slice.Coverage)
                {
                    slice.Coverage = sliceCov;
                    slice.Density = Math.Max(slice.Density, density * weight);
                    slice.Kind = kind;
                }
            }
        }

        return slices;
    }

    /// <summary>
    /// Formats a WeatherState / MetarData into the exact Coherent Metar JSON schema expected by
    /// AtmosphereMod.addon64 on the "AtmosphereMod.Metar" SimConnect CommBus channel.
    /// Verified via REA Ghidra decompilation of AtmosphereMod-0.5.1 (FUN_180038c70 &amp; FUN_180042e10):
    /// - Survey responses must echo requestLat/requestLon to 6 decimal places (&lt; 1e-5 deg match)
    /// - Primary aircraft station pushes omit root lat/lon so FUN_180044f10 applies immediately without distance-hold gating.
    /// </summary>
    public static string FormatCommBusMetarJson(WeatherState state, double? requestLat = null, double? requestLon = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        string stationId = string.IsNullOrWhiteSpace(state.StationId) ? "XXXX" : state.StationId.Trim().ToUpperInvariant();
        bool cavok = state.RawMetar.Contains("CAVOK", StringComparison.OrdinalIgnoreCase) ||
                     (state.VisibilityMeters >= 9999 && state.CloudLayers.Count == 0);

        // Coherent Metar phenomenon codes expected by AtmosphereMod (haze_mapping.h & cloud_anchor.h):
        // 1..9 = DZ/RA/SN/SG/IC/PL/GR/GS/UP, 10=BR, 11=FG, 12=FU, 13=VA, 14=DU, 15=SA, 16=HZ, 18=PO, 19=SQ, 20=FC, 21=SS, 22=DS
        var phenomEntries = BuildCoherentPhenomArray(state.RawMetar, state.PrecipitationRate, state.ThunderstormIntensity);
        var layerEntries = BuildCoherentCloudLayersArray(state.CloudLayers);

        var payload = new Dictionary<string, object?>
        {
            ["time"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["ident"] = stationId
        };

        if (requestLat.HasValue && requestLon.HasValue)
        {
            payload["lat"] = Math.Round(requestLat.Value, 6);
            payload["lon"] = Math.Round(requestLon.Value, 6);
        }

        payload["metar"] = new Dictionary<string, object?>
        {
            ["icao"] = stationId,
            ["metarString"] = !string.IsNullOrWhiteSpace(state.RawMetar)
                ? state.RawMetar
                : $"{stationId} {DateTime.UtcNow:ddHHmm}Z {(int)state.WindDirectionDegrees:D3}{(int)state.WindSpeedKnots:D2}KT {(int)Math.Min(9999, state.VisibilityMeters):D4} {(int)Math.Round(state.TemperatureCelsius):D2}/{(int)Math.Round(state.DewpointCelsius):D2} Q{(int)Math.Round(state.AltimeterHpa):D4}",
            ["temp"] = Math.Round(state.TemperatureCelsius, 1),
            ["dew"] = Math.Round(state.DewpointCelsius, 1),
            ["vis"] = Math.Round(state.VisibilityMeters, 0),
            ["visUnits"] = 0, // 0 = meters
            ["cavok"] = cavok,
            ["vwind"] = Math.Round(state.WindSpeedKnots, 1),
            ["dwind"] = Math.Round(state.WindDirectionDegrees, 0),
            ["gust"] = state.WindGustKnots.HasValue ? Math.Round(state.WindGustKnots.Value, 1) : 0.0,
            ["phenom"] = phenomEntries,
            ["layers"] = layerEntries
        };

        return JsonSerializer.Serialize(payload);
    }

    private static List<Dictionary<string, object?>> BuildCoherentPhenomArray(
        string? rawMetar,
        double precipitationRateMmHr,
        double thunderstormIntensity)
    {
        var list = new List<Dictionary<string, object?>>();
        var codes = new List<int>();
        bool ts = thunderstormIntensity >= 0.25;
        bool showers = false;
        int intensity = 0;

        if (!string.IsNullOrWhiteSpace(rawMetar))
        {
            string body = rawMetar.ToUpperInvariant();
            int rmk = body.IndexOf(" RMK", StringComparison.Ordinal);
            if (rmk > 0) body = body[..rmk];

            if (Regex.IsMatch(body, @"\b(\+|-|VC)?TS")) ts = true;
            if (Regex.IsMatch(body, @"\b(\+|-|VC)?SH")) showers = true;
            if (Regex.IsMatch(body, @"(?:^|\s)\+(DZ|RA|SN|SG|PL|GR|GS|TS|SH)")) intensity = 1;
            else if (Regex.IsMatch(body, @"(?:^|\s)-(DZ|RA|SN|SG|PL|GR|GS|TS|SH)")) intensity = -1;

            AddTokenCodeIfPresent(body, "DZ", 1, codes);
            AddTokenCodeIfPresent(body, "RA", 2, codes);
            AddTokenCodeIfPresent(body, "SN", 3, codes);
            AddTokenCodeIfPresent(body, "SG", 4, codes);
            AddTokenCodeIfPresent(body, "IC", 5, codes);
            AddTokenCodeIfPresent(body, "PL", 6, codes);
            AddTokenCodeIfPresent(body, "GR", 7, codes);
            AddTokenCodeIfPresent(body, "GS", 8, codes);
            AddTokenCodeIfPresent(body, "BR", 10, codes);
            AddTokenCodeIfPresent(body, "FG", 11, codes);
            AddTokenCodeIfPresent(body, "FU", 12, codes);
            AddTokenCodeIfPresent(body, "VA", 13, codes);
            AddTokenCodeIfPresent(body, "DU", 14, codes);
            AddTokenCodeIfPresent(body, "SA", 15, codes);
            AddTokenCodeIfPresent(body, "HZ", 16, codes);
            AddTokenCodeIfPresent(body, "PO", 18, codes);
            AddTokenCodeIfPresent(body, "SQ", 19, codes);
            AddTokenCodeIfPresent(body, "FC", 20, codes);
            AddTokenCodeIfPresent(body, "SS", 21, codes);
            AddTokenCodeIfPresent(body, "DS", 22, codes);
        }

        if (codes.Count == 0 && precipitationRateMmHr > 0.1)
        {
            codes.Add(2); // RA
        }

        if (codes.Count > 0 || ts || showers)
        {
            list.Add(new Dictionary<string, object?>
            {
                ["__Type"] = "Phenom",
                ["phenom"] = codes,
                ["ts"] = ts,
                ["showers"] = showers,
                ["intensity"] = intensity,
                ["vicinity"] = false
            });
        }

        return list;
    }

    private static void AddTokenCodeIfPresent(string body, string codeToken, int numericId, List<int> target)
    {
        if (Regex.IsMatch(body, $@"(?:^|\s)(?:\+|-|VC|MI|PR|BC|DR|BL|SH|TS|FZ)*{codeToken}(?:DZ|RA|SN|BR|FG|HZ|DU|SA|FU)*\b"))
        {
            if (!target.Contains(numericId))
                target.Add(numericId);
        }
    }

    private static List<Dictionary<string, object?>> BuildCoherentCloudLayersArray(List<CloudLayer>? cloudLayers)
    {
        var list = new List<Dictionary<string, object?>>();
        if (cloudLayers == null || cloudLayers.Count == 0)
            return list;

        foreach (var cl in cloudLayers.OrderBy(c => c.BaseFeetAgl))
        {
            if (cl.CoveragePercent <= 0.01) continue;

            // Coherent Metar cloud cover enum: 1=FEW, 2=SCT, 3=BKN, 4=OVC
            int cover = cl.CoveragePercent switch
            {
                >= 0.85 => 4,
                >= 0.55 => 3,
                >= 0.30 => 2,
                _ => 1
            };

            // Coherent Metar cloud type enum: -1=none, 0=CB, 1=TCU
            int type = (cl.IsConvective || cl.Type is CloudType.CB or CloudType.TCU)
                ? ((cl.ThunderstormIntensity ?? 0) >= 0.35 || cl.Type == CloudType.CB ? 0 : 1)
                : -1;

            // Coherent Metar layer altitude is in hundreds of feet AGL (e.g., 25 = 2500 ft)
            int altHundredsFt = Math.Max(1, (int)Math.Round(cl.BaseFeetAgl / 100.0));

            list.Add(new Dictionary<string, object?>
            {
                ["__Type"] = "Clouds",
                ["alt"] = altHundredsFt,
                ["cover"] = cover,
                ["type"] = type
            });
        }

        return list;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double SmoothStep(double e0, double e1, double x)
    {
        double t = Math.Clamp((x - e0) / (e1 - e0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }
}

/// <summary>
/// Manages %LOCALAPPDATA%\AtmosphereMod\tunables.ini, bundled shader patcher assets,
/// and SimConnect CommBus messages for AtmosphereMod.
/// </summary>
public class AtmosphereModService
{
    public const string MetarCommBusEvent = "AtmosphereMod.Metar";
    public const string SettingsCommBusEvent = "AtmosphereMod.Settings";
    public const string StateCommBusEvent = "AtmosphereMod.State";
    public const string RequestCommBusEvent = "AtmosphereMod.Request";

    private readonly string _dataFolder;
    private readonly string _tunablesPath;

    public string DataFolder => _dataFolder;
    public string TunablesFilePath => _tunablesPath;
    public AtmosphereModTunables CurrentTunables { get; private set; } = new();

    public AtmosphereModService(string? customDataFolder = null)
    {
        _dataFolder = !string.IsNullOrWhiteSpace(customDataFolder)
            ? customDataFolder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtmosphereMod");
        _tunablesPath = Path.Combine(_dataFolder, "tunables.ini");
        LoadTunables();
    }

    /// <summary>
    /// Checks whether AtmosphereMod shaders and tunables are installed in %LOCALAPPDATA%\AtmosphereMod.
    /// </summary>
    public bool IsInstalled(out int compiledShaderCount, out bool hasAddonBinary)
    {
        compiledShaderCount = 0;
        hasAddonBinary = false;

        try
        {
            if (Directory.Exists(_dataFolder))
            {
                string shadersSubdir = Path.Combine(_dataFolder, "shaders");
                if (Directory.Exists(shadersSubdir))
                {
                    compiledShaderCount = Directory.GetFiles(shadersSubdir, "*.cso", SearchOption.TopDirectoryOnly).Length;
                }
                compiledShaderCount += Directory.GetFiles(_dataFolder, "*.cso", SearchOption.TopDirectoryOnly).Length;
            }

            var bundledAddon = FindBundledAssetPath("AtmosphereMod.addon64");
            hasAddonBinary = (bundledAddon != null && File.Exists(bundledAddon)) ||
                             File.Exists(Path.Combine(_dataFolder, "AtmosphereMod.addon64")) ||
                             File.Exists(Path.Combine(_dataFolder, "install.json"));
            return File.Exists(_tunablesPath) && (compiledShaderCount > 0 || hasAddonBinary);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ensures %LOCALAPPDATA%\AtmosphereMod\tunables.ini exists (seeding from bundled defaults if missing)
    /// and loads all T0..T15 and C0..C32 values.
    /// </summary>
    public AtmosphereModTunables LoadTunables()
    {
        var tunables = new AtmosphereModTunables();

        try
        {
            Directory.CreateDirectory(_dataFolder);

            if (!File.Exists(_tunablesPath))
            {
                var bundledDefaults = FindBundledAssetPath("tunables.defaults.ini");
                if (bundledDefaults != null && File.Exists(bundledDefaults))
                {
                    File.Copy(bundledDefaults, _tunablesPath, overwrite: false);
                }
                else
                {
                    SaveTunables(tunables);
                    CurrentTunables = tunables;
                    return tunables;
                }
            }

            foreach (var rawLine in File.ReadAllLines(_tunablesPath))
            {
                var line = rawLine;
                int hashIdx = line.IndexOf('#');
                if (hashIdx >= 0) line = line[..hashIdx];
                int semiIdx = line.IndexOf(';');
                if (semiIdx >= 0) line = line[..semiIdx];

                int eqIdx = line.IndexOf('=');
                if (eqIdx <= 0) continue;

                string key = line[..eqIdx].Trim();
                string valStr = line[(eqIdx + 1)..].Trim();

                if (string.Equals(key, "enabled", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(valStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int en))
                        tunables.Enabled = en != 0;
                    else if (bool.TryParse(valStr, out bool enBool))
                        tunables.Enabled = enBool;
                    continue;
                }

                if (double.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    tunables.TrySetValue(key, val);
                }
            }
        }
        catch
        {
            // Keep defaults if tunables.ini cannot be read
        }

        CurrentTunables = tunables;
        return tunables;
    }

    /// <summary>
    /// Saves the given AtmosphereModTunables losslessly to %LOCALAPPDATA%\AtmosphereMod\tunables.ini,
    /// preserving comments and structure when the file already exists.
    /// </summary>
    public bool SaveTunables(AtmosphereModTunables tunables)
    {
        ArgumentNullException.ThrowIfNull(tunables);
        CurrentTunables = tunables.Clone();

        try
        {
            Directory.CreateDirectory(_dataFolder);

            if (!File.Exists(_tunablesPath))
            {
                var bundledDefaults = FindBundledAssetPath("tunables.defaults.ini");
                if (bundledDefaults != null && File.Exists(bundledDefaults))
                {
                    File.Copy(bundledDefaults, _tunablesPath, overwrite: true);
                }
            }

            if (File.Exists(_tunablesPath))
            {
                var lines = File.ReadAllLines(_tunablesPath).ToList();
                var writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < lines.Count; i++)
                {
                    string raw = lines[i];
                    string commentPart = string.Empty;
                    string codePart = raw;

                    int commentIdx = raw.IndexOfAny(['#', ';']);
                    if (commentIdx >= 0)
                    {
                        codePart = raw[..commentIdx];
                        commentPart = raw[commentIdx..];
                    }

                    int eqIdx = codePart.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    string key = codePart[..eqIdx].Trim();
                    if (string.Equals(key, "enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"enabled = {(tunables.Enabled ? 1 : 0)}{(string.IsNullOrEmpty(commentPart) ? "" : " " + commentPart)}";
                        writtenKeys.Add("enabled");
                    }
                    else if (tunables.TryGetValue(key, out double val))
                    {
                        string formatted = FormatTunableNumber(val);
                        lines[i] = $"{key.ToUpperInvariant()} = {formatted}{(string.IsNullOrEmpty(commentPart) ? "" : " " + commentPart)}";
                        writtenKeys.Add(key.ToUpperInvariant());
                    }
                }

                if (!writtenKeys.Contains("enabled"))
                    lines.Insert(0, $"enabled = {(tunables.Enabled ? 1 : 0)}");

                for (int i = 0; i < AtmosphereModTunables.TCount; i++)
                {
                    string k = $"T{i}";
                    if (!writtenKeys.Contains(k))
                        lines.Add($"{k} = {FormatTunableNumber(tunables.T[i])}");
                }

                for (int i = 0; i < AtmosphereModTunables.CCount; i++)
                {
                    string k = $"C{i}";
                    if (!writtenKeys.Contains(k))
                        lines.Add($"{k} = {FormatTunableNumber(tunables.C[i])}");
                }

                string tmpPath = _tunablesPath + ".tmp";
                File.WriteAllLines(tmpPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(tmpPath, _tunablesPath, overwrite: true);
                return true;
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine("# AtmosphereMod + SkyWeave Tunables Configuration");
                sb.AppendLine($"enabled = {(tunables.Enabled ? 1 : 0)}");
                for (int i = 0; i < AtmosphereModTunables.TCount; i++)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"T{i} = {FormatTunableNumber(tunables.T[i])}");
                for (int i = 0; i < AtmosphereModTunables.CCount; i++)
                    sb.AppendLine(CultureInfo.InvariantCulture, $"C{i} = {FormatTunableNumber(tunables.C[i])}");

                File.WriteAllText(_tunablesPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds a JSON payload for the "AtmosphereMod.Settings" CommBus event to push updated tunables
    /// to AtmosphereMod.addon64 in real time.
    /// </summary>
    public static string BuildSettingsSetCommandJson(AtmosphereModTunables tunables)
    {
        ArgumentNullException.ThrowIfNull(tunables);

        var setMap = new Dictionary<string, double>();
        for (int i = 0; i < AtmosphereModTunables.TCount; i++)
            setMap[$"T{i}"] = Math.Round(tunables.T[i], 4);
        for (int i = 0; i < AtmosphereModTunables.CCount; i++)
            setMap[$"C{i}"] = Math.Round(tunables.C[i], 4);

        var root = new Dictionary<string, object>
        {
            ["enabled"] = tunables.Enabled,
            ["set"] = setMap
        };

        return JsonSerializer.Serialize(root);
    }

    public static string BuildSettingsGetCommandJson() => "{\"get\":true}";
    public static string BuildSettingsReloadCommandJson() => "{\"reload\":true}";

    /// <summary>
    /// Deploys the bundled AtmosphereMod defaults and optionally runs the bundled Python DXIL shader patcher
    /// + Reshade addon installer if available on the user's system.
    /// </summary>
    public bool DeployBundledAtmosphereMod(out string statusMessage)
    {
        try
        {
            Directory.CreateDirectory(_dataFolder);

            var defaultsIni = FindBundledAssetPath("tunables.defaults.ini");
            if (defaultsIni != null && File.Exists(defaultsIni) && !File.Exists(_tunablesPath))
            {
                File.Copy(defaultsIni, _tunablesPath, overwrite: false);
            }
            else if (!File.Exists(_tunablesPath))
            {
                SaveTunables(CurrentTunables);
            }

            var addonSrc = FindBundledAssetPath("AtmosphereMod.addon64");
            string addonNote = "tunables.ini initialized";
            if (addonSrc != null && File.Exists(addonSrc))
            {
                var addonDest = Path.Combine(_dataFolder, "AtmosphereMod.addon64");
                File.Copy(addonSrc, addonDest, overwrite: true);
                addonNote = $"AtmosphereMod 0.5.1 staged in {_dataFolder}";
            }

            var installCmd = FindBundledAssetPath("Install AtmosphereMod.cmd");
            if (installCmd != null && File.Exists(installCmd))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"\"{installCmd}\"\"",
                        WorkingDirectory = Path.GetDirectoryName(installCmd) ?? _dataFolder,
                        UseShellExecute = true
                    });
                    addonNote += "; launched AtmosphereMod 0.5.1 shader patcher";
                }
                catch
                {
                    // Non-fatal if interactive console cannot be launched (e.g. headless test)
                }
            }

            statusMessage = $"AtmosphereMod 0.5.1 ready ({addonNote}). Live METAR haze, Fortuin-Kelder ozone & cloud anchoring active.";
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"AtmosphereMod deployment failed: {ex.Message}";
            return false;
        }
    }

    public static string? FindBundledAssetPath(string fileName)
    {
        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "atmosphere", fileName),
            Path.Combine(baseDir, "atmosphere", "shaders", fileName),
            Path.Combine(baseDir, "atmosphere", "tools", fileName),
            Path.Combine(baseDir, "..", "..", "..", "..", "atmosphere", fileName),
            Path.Combine(baseDir, "..", "..", "..", "..", "atmosphere", "shaders", fileName),
            Path.Combine(baseDir, "..", "..", "..", "..", "atmosphere", "tools", fileName)
        ];

        foreach (var candidate in candidates)
        {
            try
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                    return full;
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        return null;
    }

    private static string FormatTunableNumber(double v)
    {
        if (Math.Abs(v - Math.Round(v)) < 1e-6)
            return v.ToString("0.0", CultureInfo.InvariantCulture);
        return v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
