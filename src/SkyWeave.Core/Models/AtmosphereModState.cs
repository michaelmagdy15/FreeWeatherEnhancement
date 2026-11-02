using System;
using System.Collections.Generic;
using System.Globalization;

namespace SkyWeave.Core.Models;

/// <summary>
/// Air mass aerosol optical regimes classified by AtmosphereMod (haze_mapping.h).
/// </summary>
public enum AtmosphereAirMass
{
    /// <summary>Clean / continental background air (Ångström α ≈ 1.3, g ≈ 0.70, brown carbon ≈ 0.05).</summary>
    Continental = 0,
    /// <summary>Humid / urban haze (BR/FG/HZ — water-swollen sulfate/nitrate droplets, α ≈ 0.3–1.4, g ≈ 0.72).</summary>
    Haze = 1,
    /// <summary>Biomass / wildfire smoke (FU — fine carbonaceous particles, α ≈ 1.8, g ≈ 0.65, brown carbon ≈ 0.25).</summary>
    Smoke = 2,
    /// <summary>Mineral dust / sand (DU/SA/DS/SS/PO — coarse desert dust, α ≈ 0.2, g ≈ 0.76, brown carbon ≈ 0.30).</summary>
    Dust = 3,
    /// <summary>Volcanic ash (VA — coarse dark silicate shards, α ≈ 0.1, g ≈ 0.72, brown carbon ≈ 0.20).</summary>
    VolcanicAsh = 4
}

/// <summary>
/// Cloud family classification used by AtmosphereMod cloud anchoring (cloud_anchor.h).
/// </summary>
public enum AtmosphereCloudKind
{
    /// <summary>Fair-weather cumuliform (FEW/SCT non-convective).</summary>
    Cumuliform = 0,
    /// <summary>Broken/overcast stratiform deck (BKN/OVC or base &lt; 6500 ft).</summary>
    Stratiform = 1,
    /// <summary>Towering cumulus / cumulonimbus convective cell (CB/TCU).</summary>
    Convective = 2,
    /// <summary>Smooth stratus/fog deck under mist/fog/low overcast.</summary>
    SmoothDeck = 3
}

/// <summary>
/// One of the 32 vertical altitude slices (z_m = 15.625 * k^2) in MSFS 2024's 3D weather-map texture.
/// </summary>
public class AtmosphereCloudSlice
{
    public int Index { get; set; }
    public double AltitudeMetersMsl { get; set; }
    public double Coverage { get; set; }
    public double Density { get; set; }
    public AtmosphereCloudKind Kind { get; set; }
}

/// <summary>
/// Derived atmospheric optics, boundary-layer haze, ozone column, and 32-slice cloud anchor state
/// produced from live METAR + solar/geographic coordinates via AtmosphereMod physics.
/// </summary>
public class AtmosphereModState
{
    /// <summary>Classified dominant aerosol regime (Continental, Haze, Smoke, Dust, VolcanicAsh).</summary>
    public AtmosphereAirMass AirMass { get; set; } = AtmosphereAirMass.Continental;

    /// <summary>Human-readable air-mass label (e.g., "Continental", "Haze", "Smoke", "Dust", "VolcanicAsh").</summary>
    public string AirMassLabel => AirMass.ToString();

    /// <summary>Relative humidity [0..1] computed via the Magnus formula.</summary>
    public double RelativeHumidity01 { get; set; }

    /// <summary>T1: Boundary-layer top altitude in km MSL.</summary>
    public double BoundaryLayerTopKmMsl { get; set; } = 1.5;

    /// <summary>T2: Boundary-layer transition edge half-width in km.</summary>
    public double BoundaryLayerEdgeWidthKm { get; set; } = 0.15;

    /// <summary>T3: Boundary-layer aerosol extinction multiplier relative to MSFS stock reference.</summary>
    public double BoundaryLayerHazeMultiplier { get; set; } = 4.0;

    /// <summary>T4: Free-troposphere aerosol extinction multiplier above the boundary layer.</summary>
    public double FreeTroposphereHazeMultiplier { get; set; } = 0.6;

    /// <summary>T5: Free-troposphere aerosol exponential scale height in km.</summary>
    public double FreeTroposphereScaleHeightKm { get; set; } = 2.5;

    /// <summary>T6: Brown carbon / mineral dust short-wave absorption strength.</summary>
    public double BrownCarbonAbsorption { get; set; } = 0.05;

    /// <summary>T9: Double-Henyey-Greenstein forward scattering asymmetry parameter g.</summary>
    public double AerosolAsymmetryG { get; set; } = 0.70;

    /// <summary>T10: Ångström wavelength exponent α (higher = bluer fine-mode haze, lower = whiter/warmer coarse-mode).</summary>
    public double AngstromExponent { get; set; } = 1.30;

    /// <summary>T14: Total column ozone in Dobson Units (Fortuin &amp; Kelder 1998 zonal climatology or manual override).</summary>
    public double OzoneDobsonUnits { get; set; } = 300.0;

    /// <summary>True when dry haze was inferred from restricted visibility despite low relative humidity.</summary>
    public bool InferredDryHaze { get; set; }

    /// <summary>Active thunderstorm/convective tower evidence [0..1] driving C32 CB tower anchoring.</summary>
    public double StormTowerStrength { get; set; }

    /// <summary>Summary string formatted identically to AtmosphereMod's in-sim EFB status line.</summary>
    public string StatusSummary { get; set; } = string.Empty;

    /// <summary>Active 32-slice cloud anchor profile for the station.</summary>
    public List<AtmosphereCloudSlice> CloudAnchorSlices { get; set; } = new();

    public AtmosphereModState Clone()
    {
        var copy = new AtmosphereModState
        {
            AirMass = AirMass,
            RelativeHumidity01 = RelativeHumidity01,
            BoundaryLayerTopKmMsl = BoundaryLayerTopKmMsl,
            BoundaryLayerEdgeWidthKm = BoundaryLayerEdgeWidthKm,
            BoundaryLayerHazeMultiplier = BoundaryLayerHazeMultiplier,
            FreeTroposphereHazeMultiplier = FreeTroposphereHazeMultiplier,
            FreeTroposphereScaleHeightKm = FreeTroposphereScaleHeightKm,
            BrownCarbonAbsorption = BrownCarbonAbsorption,
            AerosolAsymmetryG = AerosolAsymmetryG,
            AngstromExponent = AngstromExponent,
            OzoneDobsonUnits = OzoneDobsonUnits,
            InferredDryHaze = InferredDryHaze,
            StormTowerStrength = StormTowerStrength,
            StatusSummary = StatusSummary,
            CloudAnchorSlices = new List<AtmosphereCloudSlice>(CloudAnchorSlices.Count)
        };

        foreach (var slice in CloudAnchorSlices)
        {
            copy.CloudAnchorSlices.Add(new AtmosphereCloudSlice
            {
                Index = slice.Index,
                AltitudeMetersMsl = slice.AltitudeMetersMsl,
                Coverage = slice.Coverage,
                Density = slice.Density,
                Kind = slice.Kind
            });
        }

        return copy;
    }
}

/// <summary>
/// Complete set of 49 shader &amp; add-on tunables (T0..T15 and C0..C32) for AtmosphereMod,
/// matching %LOCALAPPDATA%\AtmosphereMod\tunables.ini and the AtmosphereMod.Settings CommBus protocol.
/// </summary>
public class AtmosphereModTunables
{
    public const int TCount = 16;
    public const int CCount = 33;

    public bool Enabled { get; set; } = true;
    public double[] T { get; } = new double[TCount];
    public double[] C { get; } = new double[CCount];

    public static readonly double[] DefaultT =
    [
        1.0,    // T0: Atmosphere changes master switch (1=mod, 0=stock)
        1.5,    // T1: Boundary layer top altitude (km MSL)
        0.15,   // T2: Boundary layer transition edge half-width (km)
        4.0,    // T3: Boundary layer haze amount
        0.6,    // T4: Free troposphere haze amount
        2.5,    // T5: Free troposphere exponential scale height (km)
        0.05,   // T6: Brown carbon / dust colour tint
        0.1,    // T7: Aerosol backscatter lobe weight
        1.0,    // T8: Full-spectrum sky & sunlight colour (Rayleigh + Chappuis ozone)
        0.7,    // T9: Aerosol forward scattering asymmetry g
        1.3,    // T10: Ångström exponent (haze colour)
        1.0,    // T11: Distant clouds fade per wavelength
        1.0,    // T12: Distant haze reach beyond 357 km
        1.0,    // T13: Weather-driven haze from live METAR
        0.0,    // T14: Ozone amount in Dobson Units (0 = automatic Fortuin & Kelder climatology)
        1.0     // T15: Smooth multi-station haze transitions
    ];

    public static readonly double[] DefaultC =
    [
        1.0,    // C0: Cloud lighting changes master switch
        0.9,    // C1: Forward scattering lobe g
        0.5,    // C2: Bright cloud rims (silver lining weight)
        0.4,    // C3: Light bouncing inside clouds
        0.4,    // C4: Forward bias of bounced light
        0.12,   // C5: Light reaching deep cloud interiors
        0.5,    // C6: Darker cloud edges (powder effect strength)
        150.0,  // C7: Powder build-up depth (m)
        0.0,    // C8: Crisper cloud edges
        0.5,    // C9: Glory ring strength around aircraft shadow
        4.0,    // C10: Glory droplet radius (µm)
        0.08,   // C11: Glory droplet size spread
        0.4,    // C12: Aircraft shadow darkness on clouds
        0.0,    // C13: Reserved
        0.0,    // C14: Reserved
        0.0,    // C15: Reserved
        2.0,    // C16: Solid cloud thickness
        0.33,   // C17: Cloud base density profile exponent
        0.0,    // C18: High-cloud transition altitude (m, 0 = 6000m)
        1.5,    // C19: High-cloud (cirrus) thickness multiplier
        1.0,    // C20: Match clouds to live METAR (cloud anchoring strength)
        50.0,   // C21: Airport weather reach radius (km)
        1.0,    // C22: Daylight colour balance (remove yellow tint)
        1.0,    // C23: Distinct cloud types master switch
        0.6,    // C24: Stratocumulus thickness share
        0.5,    // C25: Stratus thickness share
        0.4,    // C26: Altocumulus thickness share
        0.3,    // C27: Altostratus thickness share
        1.0,    // C28: Smooth flat cloud bases
        300.0,  // C29: Cloud base smoothing height (m)
        0.0,    // C30: Finer cloud-top billows
        600.0,  // C31: Cloud-top billow size (m)
        0.0     // C32: Experimental towering thunderstorm clouds (supercell/CB)
    ];

    public AtmosphereModTunables()
    {
        ResetToDefaults();
    }

    public void ResetToDefaults()
    {
        Enabled = true;
        Array.Copy(DefaultT, T, TCount);
        Array.Copy(DefaultC, C, CCount);
    }

    public AtmosphereModTunables Clone()
    {
        var copy = new AtmosphereModTunables { Enabled = Enabled };
        Array.Copy(T, copy.T, TCount);
        Array.Copy(C, copy.C, CCount);
        return copy;
    }

    public bool TryGetValue(string key, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(key) || key.Length < 2)
            return false;

        char prefix = char.ToUpperInvariant(key[0]);
        if (!int.TryParse(key.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx) || idx < 0)
            return false;

        if (prefix == 'T' && idx < TCount)
        {
            value = T[idx];
            return true;
        }

        if (prefix == 'C' && idx < CCount)
        {
            value = C[idx];
            return true;
        }

        return false;
    }

    public bool TrySetValue(string key, double value)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length < 2 || !double.IsFinite(value))
            return false;

        char prefix = char.ToUpperInvariant(key[0]);
        if (!int.TryParse(key.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx) || idx < 0)
            return false;

        if (prefix == 'T' && idx < TCount)
        {
            T[idx] = value;
            return true;
        }

        if (prefix == 'C' && idx < CCount)
        {
            C[idx] = value;
            return true;
        }

        return false;
    }
}
