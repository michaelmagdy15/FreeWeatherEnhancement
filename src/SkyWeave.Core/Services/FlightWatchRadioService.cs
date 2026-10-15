using System.Text;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public enum FlightWatchChannel
{
    None = 0,
    EnrouteFlightWatch12200 = 1,
    DestinationAtis122025 = 2,
    DepartureAtis122050 = 3
}

public sealed record FlightWatchTransmission(
    FlightWatchChannel Channel,
    string FrequencyDisplay,
    string StationId,
    string CallsignHeader,
    string DisplayTranscript,
    string SpokenPhoneticScript,
    DateTime GeneratedAtUtc);

/// <summary>
/// Active Sky Next-inspired VHF Flight Watch & Voice ATIS Radio Service.
/// Monitors COM1/COM2 frequencies (122.000 MHz Enroute Flight Watch, 122.025 MHz Destination ATIS,
/// 122.050 MHz Departure ATIS) and generates authentic FAA/ICAO phonetic voice scripts and
/// VHF squelch-Burst WAV audio cues.
/// </summary>
public class FlightWatchRadioService
{
    public const double EnrouteFlightWatchFreqMhz = 122.000;
    public const double DestinationAtisFreqMhz = 122.025;
    public const double DepartureAtisFreqMhz = 122.050;
    private const double FrequencyToleranceMhz = 0.008;

    public FlightWatchChannel DetectTunedChannel(double com1Mhz, double com2Mhz)
    {
        var ch1 = MatchFrequency(com1Mhz);
        if (ch1 != FlightWatchChannel.None)
            return ch1;

        return MatchFrequency(com2Mhz);
    }

    public static FlightWatchChannel MatchFrequency(double freqMhz)
    {
        if (freqMhz <= 0) return FlightWatchChannel.None;
        if (Math.Abs(freqMhz - EnrouteFlightWatchFreqMhz) <= FrequencyToleranceMhz)
            return FlightWatchChannel.EnrouteFlightWatch12200;
        if (Math.Abs(freqMhz - DestinationAtisFreqMhz) <= FrequencyToleranceMhz)
            return FlightWatchChannel.DestinationAtis122025;
        if (Math.Abs(freqMhz - DepartureAtisFreqMhz) <= FrequencyToleranceMhz)
            return FlightWatchChannel.DepartureAtis122050;
        return FlightWatchChannel.None;
    }

    public FlightWatchTransmission BuildTransmission(
        WeatherState? state,
        FlightWatchChannel channel = FlightWatchChannel.EnrouteFlightWatch12200,
        double aircraftAltitudeFeet = 0,
        WeatherState? destinationState = null,
        WeatherState? departureState = null)
    {
        var activeState = channel switch
        {
            FlightWatchChannel.DestinationAtis122025 => destinationState ?? state,
            FlightWatchChannel.DepartureAtis122050 => departureState ?? state,
            _ => state
        };

        if (activeState == null || string.IsNullOrWhiteSpace(activeState.StationId))
        {
            return new FlightWatchTransmission(
                channel == FlightWatchChannel.None ? FlightWatchChannel.EnrouteFlightWatch12200 : channel,
                FormatFrequency(channel),
                "UNKN",
                "SKYWEAVE FLIGHT WATCH",
                "SKYWEAVE FLIGHT WATCH STANDING BY. AWAITING LIVE AIRCRAFT TELEMETRY AND METAR OBSERVATION.",
                "SkyWeave Flight Watch standing by on one two two point zero. Awaiting live aircraft telemetry and station observation.",
                DateTime.UtcNow);
        }

        var station = activeState.StationId.ToUpperInvariant();
        var zuluTime = activeState.ObservationTime == default
            ? DateTime.UtcNow.ToString("HHmm")
            : activeState.ObservationTime.ToUniversalTime().ToString("HHmm");

        var freqDisplay = FormatFrequency(channel);
        var header = channel switch
        {
            FlightWatchChannel.DestinationAtis122025 => $"{station} DESTINATION ATIS ({freqDisplay})",
            FlightWatchChannel.DepartureAtis122050 => $"{station} DEPARTURE ATIS ({freqDisplay})",
            _ => $"SKYWEAVE FLIGHT WATCH {station} ({freqDisplay})"
        };

        var windDir = ((int)Math.Round(activeState.WindDirectionDegrees) % 360 + 360) % 360;
        var windSpd = (int)Math.Round(activeState.WindSpeedKnots);
        var gustSpd = activeState.WindGustKnots.HasValue ? (int)Math.Round(activeState.WindGustKnots.Value) : 0;
        var visSm = activeState.VisibilityMeters / 1609.344;
        var tempC = (int)Math.Round(activeState.TemperatureCelsius);
        var dewpC = (int)Math.Round(activeState.DewpointCelsius);
        var qnhHpa = (int)Math.Round(activeState.AltimeterHpa);
        var altimInHg = activeState.AltimeterHpa / 33.8639;
        var altimDigits = (int)Math.Round(altimInHg * 100);

        var isNorthAmerica = station.StartsWith("K", StringComparison.Ordinal) ||
                             station.StartsWith("C", StringComparison.Ordinal) ||
                             station.StartsWith("P", StringComparison.Ordinal);

        // 1. Build concise display transcript
        var displaySb = new StringBuilder();
        displaySb.Append($"{header} // {zuluTime}Z // {activeState.FlightCategory} // ");
        displaySb.Append(windSpd <= 1
            ? "WIND CALM"
            : gustSpd > windSpd
                ? $"WIND {windDir:D3} AT {windSpd}KT GUSTS {gustSpd}KT"
                : $"WIND {windDir:D3} AT {windSpd}KT");

        displaySb.Append(visSm >= 9.9 ? " // VIS 10SM+" : $" // VIS {visSm:F1}SM ({activeState.VisibilityMeters:F0}M)");
        displaySb.Append($" // TEMP {tempC}C DEWP {dewpC}C");
        displaySb.Append(isNorthAmerica
            ? $" // ALTIMETER {altimDigits:D4} ({qnhHpa} HPA)"
            : $" // QNH {qnhHpa} HPA ({altimDigits:D4})");

        if (activeState.CloudLayers.Count > 0)
        {
            var cloudSummary = string.Join(", ", activeState.CloudLayers.Take(3).Select(c => $"{c.Type} {c.BaseFeetAgl:F0}FT"));
            displaySb.Append($" // CLOUDS {cloudSummary}");
        }
        else
        {
            displaySb.Append(" // SKY CLEAR");
        }

        // 2. Build spoken phonetic script
        var spokenSb = new StringBuilder();
        spokenSb.Append(channel switch
        {
            FlightWatchChannel.DestinationAtis122025 => $"{ToPhoneticIcao(station)} destination weather information, observation time {ToSpokenDigits(zuluTime)} Zulu. ",
            FlightWatchChannel.DepartureAtis122050 => $"{ToPhoneticIcao(station)} departure weather information, observation time {ToSpokenDigits(zuluTime)} Zulu. ",
            _ => $"SkyWeave Enroute Flight Watch, nearest reporting station {ToPhoneticIcao(station)}, observation {ToSpokenDigits(zuluTime)} Zulu. "
        });

        if (windSpd <= 1)
        {
            spokenSb.Append("Wind calm. ");
        }
        else if (gustSpd > windSpd)
        {
            spokenSb.Append($"Wind {ToSpokenDigits(windDir.ToString("D3"))} at {ToSpokenNumber(windSpd)} knots, gusting {ToSpokenNumber(gustSpd)}. ");
        }
        else
        {
            spokenSb.Append($"Wind {ToSpokenDigits(windDir.ToString("D3"))} at {ToSpokenNumber(windSpd)} knots. ");
        }

        if (visSm >= 9.9)
        {
            spokenSb.Append("Visibility one zero miles or greater. ");
        }
        else if (visSm < 1.0)
        {
            spokenSb.Append($"Visibility low, {activeState.VisibilityMeters:F0} meters. ");
        }
        else
        {
            spokenSb.Append($"Visibility {ToSpokenNumber((int)Math.Round(visSm))} miles. ");
        }

        if (activeState.CloudLayers.Count == 0)
        {
            spokenSb.Append("Sky clear. ");
        }
        else
        {
            foreach (var layer in activeState.CloudLayers.Take(3))
            {
                var covWord = layer.Type switch
                {
                    CloudType.FEW => "Few clouds",
                    CloudType.SCT => "Scattered",
                    CloudType.BKN => "Broken ceiling",
                    CloudType.OVC => "Overcast ceiling",
                    CloudType.CB => "Cumulonimbus",
                    CloudType.TCU => "Towering cumulus",
                    _ => "Clouds"
                };
                var roundedBase = (int)(Math.Round(layer.BaseFeetAgl / 100.0) * 100);
                spokenSb.Append($"{covWord} at {roundedBase} feet. ");
            }
        }

        spokenSb.Append($"Temperature {FormatSpokenSignedInt(tempC)}, dewpoint {FormatSpokenSignedInt(dewpC)}. ");
        if (isNorthAmerica)
        {
            spokenSb.Append($"Altimeter {ToSpokenDigits(altimDigits.ToString("D4"))}. ");
        }
        else
        {
            spokenSb.Append($"Q N H {ToSpokenDigits(qnhHpa.ToString())}. ");
        }

        // Include winds aloft & hazard advisory on Enroute Flight Watch (122.00)
        if (channel is FlightWatchChannel.EnrouteFlightWatch12200 or FlightWatchChannel.None)
        {
            var targetAlt = aircraftAltitudeFeet > 1000 ? aircraftAltitudeFeet : 10000;
            var aloft = activeState.WindsAloft
                .Where(w => !w.IsSurfaceLayer)
                .MinBy(w => Math.Abs(w.AltitudeFeet - targetAlt));

            if (aloft != null)
            {
                var aloftDir = ((int)Math.Round(aloft.DirectionDegrees) % 360 + 360) % 360;
                var aloftSpd = (int)Math.Round(aloft.SpeedKnots);
                var aloftFl = (int)Math.Round(aloft.AltitudeFeet / 100.0);
                displaySb.Append($" // ALOFT FL{aloftFl:D3}: {aloftDir:D3}/{aloftSpd}KT ({aloft.TemperatureCelsius:F0}C)");
                spokenSb.Append($"Winds aloft at { (int)Math.Round(aloft.AltitudeFeet / 1000.0) } thousand feet: {ToSpokenDigits(aloftDir.ToString("D3"))} at {ToSpokenNumber(aloftSpd)} knots, temperature {FormatSpokenSignedInt((int)Math.Round(aloft.TemperatureCelsius))}. ");
            }

            if (activeState.TurbulenceIndex >= 0.35)
            {
                spokenSb.Append("Caution: moderate to severe turbulence reported along route. ");
            }

            if (activeState.IcingIndex >= 0.35)
            {
                spokenSb.Append($"Airframe icing advisory active, freezing level {(int)Math.Round(activeState.FreezingLevelFeet)} feet. ");
            }

            if (activeState.StructuralIcePercent.HasValue && activeState.StructuralIcePercent.Value >= 5.0)
            {
                spokenSb.Append($"Warning: structural ice accumulation {activeState.StructuralIcePercent.Value:F0} percent detected on airframe. ");
            }
        }

        return new FlightWatchTransmission(
            channel == FlightWatchChannel.None ? FlightWatchChannel.EnrouteFlightWatch12200 : channel,
            freqDisplay,
            station,
            header,
            displaySb.ToString(),
            spokenSb.ToString().Trim(),
            DateTime.UtcNow);
    }

    /// <summary>
    /// Synthesizes a short VHF AM radio squelch + carrier burst WAV buffer (16-bit mono 16kHz PCM)
    /// to frame voice transmissions with authentic cockpit radio acoustics.
    /// </summary>
    public byte[] GenerateVhfSquelchBurstWav(int durationMs = 280)
    {
        const int sampleRate = 16000;
        var numSamples = Math.Clamp(sampleRate * durationMs / 1000, 800, sampleRate * 2);
        var dataBytes = numSamples * 2;

        using var ms = new MemoryStream(44 + dataBytes);
        using var bw = new BinaryWriter(ms);

        // RIFF WAVE header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataBytes);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)1); // Mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataBytes);

        var rng = new Random(12200);
        double bpState = 0;
        for (int i = 0; i < numSamples; i++)
        {
            var t = (double)i / sampleRate;
            var env = i < numSamples * 0.18
                ? 0.45 // Initial PTT click/squelch tail
                : 0.08 * Math.Exp(-t * 8.0);
            var white = (rng.NextDouble() * 2.0) - 1.0;
            bpState = 0.65 * bpState + 0.35 * white;
            var tone = Math.Sin(2.0 * Math.PI * 1020.0 * t) * 0.12;
            var sample = Math.Clamp((bpState + tone) * env, -0.95, 0.95);
            bw.Write((short)(sample * short.MaxValue));
        }

        return ms.ToArray();
    }

    public static string ToPhoneticIcao(string icao)
    {
        if (string.IsNullOrWhiteSpace(icao)) return "Unknown Station";
        var words = new List<string>(icao.Length);
        foreach (var ch in icao.ToUpperInvariant())
        {
            words.Add(ch switch
            {
                'A' => "Alpha",
                'B' => "Bravo",
                'C' => "Charlie",
                'D' => "Delta",
                'E' => "Echo",
                'F' => "Foxtrot",
                'G' => "Golf",
                'H' => "Hotel",
                'I' => "India",
                'J' => "Juliet",
                'K' => "Kilo",
                'L' => "Lima",
                'M' => "Mike",
                'N' => "November",
                'O' => "Oscar",
                'P' => "Papa",
                'Q' => "Quebec",
                'R' => "Romeo",
                'S' => "Sierra",
                'T' => "Tango",
                'U' => "Uniform",
                'V' => "Victor",
                'W' => "Whiskey",
                'X' => "X-Ray",
                'Y' => "Yankee",
                'Z' => "Zulu",
                _ => ch.ToString()
            });
        }
        return string.Join(" ", words);
    }

    public static string ToSpokenDigits(string digits)
    {
        var words = new List<string>(digits.Length);
        foreach (var ch in digits)
        {
            words.Add(ch switch
            {
                '0' => "zero",
                '1' => "one",
                '2' => "two",
                '3' => "tree",
                '4' => "fower",
                '5' => "fife",
                '6' => "six",
                '7' => "seven",
                '8' => "eight",
                '9' => "niner",
                _ => ch.ToString()
            });
        }
        return string.Join(" ", words);
    }

    private static string ToSpokenNumber(int value)
    {
        if (value < 0) return "minus " + ToSpokenDigits(Math.Abs(value).ToString());
        return ToSpokenDigits(value.ToString());
    }

    private static string FormatSpokenSignedInt(int value)
    {
        if (value < 0) return "minus " + ToSpokenDigits(Math.Abs(value).ToString());
        return ToSpokenDigits(value.ToString());
    }

    private static string FormatFrequency(FlightWatchChannel channel) => channel switch
    {
        FlightWatchChannel.DestinationAtis122025 => "122.025 MHz",
        FlightWatchChannel.DepartureAtis122050 => "122.050 MHz",
        _ => "122.000 MHz"
    };
}
