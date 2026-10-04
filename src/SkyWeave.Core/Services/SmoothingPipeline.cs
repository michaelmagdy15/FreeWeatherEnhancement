using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class SmoothingPipeline
{
    private WeatherState? _current;
    private WeatherState? _target;
    private DateTime _transitionStart;
    private TimeSpan _transitionDuration = TimeSpan.FromMinutes(3);
    private readonly object _lock = new();

    public TimeSpan TransitionDuration
    {
        get => _transitionDuration;
        set => _transitionDuration = value;
    }

    /// <summary>
    /// Maximum allowed rate of change for wind speed in knots per second.
    /// Prevents abrupt wind shifts that disconnect airliner autopilots.
    /// </summary>
    public double MaxWindSpeedRateKtPerSec { get; set; } = 5.0;

    /// <summary>
    /// Maximum allowed rate of change for wind direction in degrees per second along the shortest circular arc.
    /// Clamps heading swing rate to protect aircraft roll stability.
    /// </summary>
    public double MaxWindDirRateDegPerSec { get; set; } = 7.5;

    private double _simulationRate = 1.0;

    /// <summary>
    /// Current simulation rate multiplier (e.g. 1.0x, 2.0x, 4.0x, 8.0x, 16.0x).
    /// Scales wind slew clamp rates proportionally during time compression so that
    /// the aircraft experiences smooth physical changes relative to simulated flight time.
    /// Clamped between 1.0 and 16.0.
    /// </summary>
    public double SimulationRate
    {
        get => _simulationRate;
        set => _simulationRate = Math.Clamp(value, 1.0, 16.0);
    }

    public bool IsTransitioning
    {
        get
        {
            lock (_lock)
            {
                if (_target == null) return false;
                var elapsed = DateTime.UtcNow - _transitionStart;
                if (elapsed < _transitionDuration) return true;

                var current = GetCurrentState();
                if (Math.Abs(current.WindSpeedKnots - _target.WindSpeedKnots) > 0.1) return true;
                var diff = Math.Abs(_target.WindDirectionDegrees - current.WindDirectionDegrees);
                if (diff > 180) diff = 360 - diff;
                if (diff > 0.5) return true;

                return false;
            }
        }
    }

    public void SetTarget(WeatherState target)
    {
        lock (_lock)
        {
            if (_current == null)
            {
                _current = CloneState(target);
                _target = CloneState(target);
                return;
            }

            _current = GetCurrentState();
            _target = CloneState(target);
            _transitionStart = DateTime.UtcNow;
        }
    }

    public WeatherState GetCurrentState()
    {
        lock (_lock)
        {
            if (_current == null || _target == null)
                return _current ?? new WeatherState();

            var elapsed = DateTime.UtcNow - _transitionStart;
            var t = Math.Min(1.0, elapsed.TotalSeconds / Math.Max(0.001, _transitionDuration.TotalSeconds));
            t = SmoothStep(t);

            return Interpolate(_current, _target, t, elapsed.TotalSeconds);
        }
    }

    public void ForceImmediate()
    {
        lock (_lock)
        {
            if (_target != null)
            {
                _current = CloneState(_target);
                _target = null;
            }
        }
    }

    private double SmoothStep(double t)
    {
        return t * t * (3 - 2 * t);
    }

    public double SlewClampSpeed(double fromSpeed, double toSpeed, double t, double elapsedSeconds)
    {
        var targetSpeed = Lerp(fromSpeed, toSpeed, t);
        if (MaxWindSpeedRateKtPerSec <= 0 || elapsedSeconds <= 0)
            return targetSpeed;

        var effectiveRate = MaxWindSpeedRateKtPerSec * SimulationRate;
        var maxDelta = effectiveRate * elapsedSeconds;
        var diff = targetSpeed - fromSpeed;
        if (Math.Abs(diff) > maxDelta)
        {
            targetSpeed = fromSpeed + Math.Sign(diff) * maxDelta;
        }
        return Math.Max(0, targetSpeed);
    }

    public double SlewClampAngle(double fromAngle, double toAngle, double t, double elapsedSeconds)
    {
        var targetAngle = LerpAngle(fromAngle, toAngle, t);
        if (MaxWindDirRateDegPerSec <= 0 || elapsedSeconds <= 0)
            return targetAngle;

        var diff = targetAngle - fromAngle;
        while (diff > 180) diff -= 360;
        while (diff < -180) diff += 360;

        var effectiveRate = MaxWindDirRateDegPerSec * SimulationRate;
        var maxDelta = effectiveRate * elapsedSeconds;
        if (Math.Abs(diff) > maxDelta)
        {
            var clamped = fromAngle + Math.Sign(diff) * maxDelta;
            while (clamped < 0) clamped += 360;
            while (clamped >= 360) clamped -= 360;
            return clamped;
        }

        return targetAngle;
    }

    private WeatherState Interpolate(WeatherState from, WeatherState to, double t, double elapsedSeconds)
    {
        var result = new WeatherState
        {
            ObservationTime = to.ObservationTime,
            StationId = to.StationId,
            RawMetar = to.RawMetar,
            Taf = to.Taf,
            Latitude = to.Latitude,
            Longitude = to.Longitude,
            FlightCategory = to.FlightCategory,
            Precipitation = to.Precipitation,

            TemperatureCelsius = Lerp(from.TemperatureCelsius, to.TemperatureCelsius, t),
            DewpointCelsius = Lerp(from.DewpointCelsius, to.DewpointCelsius, t),
            PressureHpa = Lerp(from.PressureHpa, to.PressureHpa, t),
            AltimeterHpa = Lerp(from.AltimeterHpa, to.AltimeterHpa, t),
            VisibilityMeters = Lerp(from.VisibilityMeters, to.VisibilityMeters, t),
            WindDirectionDegrees = SlewClampAngle(from.WindDirectionDegrees, to.WindDirectionDegrees, t, elapsedSeconds),
            WindSpeedKnots = SlewClampSpeed(from.WindSpeedKnots, to.WindSpeedKnots, t, elapsedSeconds),
            WindGustKnots = NullableLerp(from.WindGustKnots, to.WindGustKnots, t),
            GustDirectionDegrees = NullableLerpAngle(from.GustDirectionDegrees, to.GustDirectionDegrees, t),
            HumidityPercent = Lerp(from.HumidityPercent, to.HumidityPercent, t),
            FreezingLevelFeet = Lerp(from.FreezingLevelFeet, to.FreezingLevelFeet, t),
            CeilingFeet = Lerp(from.CeilingFeet, to.CeilingFeet, t),
            PrecipitationRate = Lerp(from.PrecipitationRate, to.PrecipitationRate, t),
            IcingIndex = Lerp(from.IcingIndex, to.IcingIndex, t),
            TurbulenceIndex = Lerp(from.TurbulenceIndex, to.TurbulenceIndex, t),
            ThunderstormIntensity = Lerp(from.ThunderstormIntensity, to.ThunderstormIntensity, t),
            AerosolDensity = Lerp(from.AerosolDensity, to.AerosolDensity, t),

            SourceModelName = to.SourceModelName,
            DataAgeMinutes = to.DataAgeMinutes,
            ConvectiveAvailablePotentialEnergy = to.ConvectiveAvailablePotentialEnergy,
            LiftedIndex = to.LiftedIndex,
            CloudLayers = to.CloudLayers,
            WindsAloft = InterpolateWindsAloft(from.WindsAloft, to.WindsAloft, t, elapsedSeconds),
            Hazards = to.Hazards,
            StormCells = to.StormCells,
            IcingLayers = to.IcingLayers,
            TurbulenceLayers = to.TurbulenceLayers
        };

        return result;
    }

    private List<WindLayer> InterpolateWindsAloft(
        List<WindLayer> fromLayers,
        List<WindLayer> toLayers,
        double t,
        double elapsedSeconds)
    {
        if (toLayers == null || toLayers.Count == 0)
            return fromLayers != null ? CloneWindLayers(fromLayers) : new List<WindLayer>();
        if (fromLayers == null || fromLayers.Count == 0)
            return CloneWindLayers(toLayers);

        var result = new List<WindLayer>(toLayers.Count);
        foreach (var toLayer in toLayers)
        {
            var match = fromLayers.MinBy(fl => Math.Abs(fl.AltitudeFeet - toLayer.AltitudeFeet));
            if (match == null)
            {
                result.Add(CloneWindLayer(toLayer));
                continue;
            }

            var speed = SlewClampSpeed(match.SpeedKnots, toLayer.SpeedKnots, t, elapsedSeconds);
            var dir = SlewClampAngle(match.DirectionDegrees, toLayer.DirectionDegrees, t, elapsedSeconds);

            result.Add(new WindLayer
            {
                Id = toLayer.Id,
                IsSurfaceLayer = toLayer.IsSurfaceLayer,
                AltitudeFeet = Lerp(match.AltitudeFeet, toLayer.AltitudeFeet, t),
                AltitudeMeters = Lerp(match.AltitudeMeters, toLayer.AltitudeMeters, t),
                DirectionDegrees = dir,
                SpeedKnots = speed,
                GustSpeedKnots = NullableLerp(match.GustSpeedKnots, toLayer.GustSpeedKnots, t),
                GustDirectionDegrees = NullableLerpAngle(match.GustDirectionDegrees, toLayer.GustDirectionDegrees, t),
                TemperatureCelsius = Lerp(match.TemperatureCelsius, toLayer.TemperatureCelsius, t),
                TurbulenceIntensity = NullableLerp(match.TurbulenceIntensity, toLayer.TurbulenceIntensity, t)
            });
        }

        return result;
    }

    private static List<WindLayer> CloneWindLayers(IEnumerable<WindLayer> layers)
    {
        return layers.Select(CloneWindLayer).ToList();
    }

    private static WindLayer CloneWindLayer(WindLayer layer)
    {
        return new WindLayer
        {
            Id = layer.Id,
            IsSurfaceLayer = layer.IsSurfaceLayer,
            AltitudeMeters = layer.AltitudeMeters,
            AltitudeFeet = layer.AltitudeFeet,
            DirectionDegrees = layer.DirectionDegrees,
            SpeedKnots = layer.SpeedKnots,
            GustSpeedKnots = layer.GustSpeedKnots,
            GustDirectionDegrees = layer.GustDirectionDegrees,
            TemperatureCelsius = layer.TemperatureCelsius,
            TurbulenceIntensity = layer.TurbulenceIntensity
        };
    }

    private double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    private double LerpAngle(double a, double b, double t)
    {
        var diff = b - a;
        if (diff > 180) diff -= 360;
        if (diff < -180) diff += 360;
        var result = a + diff * t;
        if (result < 0) result += 360;
        if (result >= 360) result -= 360;
        return result;
    }

    private double? NullableLerp(double? a, double? b, double t)
    {
        if (!a.HasValue && !b.HasValue) return null;
        if (!a.HasValue) return b;
        if (!b.HasValue) return a;
        return Lerp(a.Value, b.Value, t);
    }

    private double? NullableLerpAngle(double? a, double? b, double t)
    {
        if (!a.HasValue && !b.HasValue) return null;
        if (!a.HasValue) return b;
        if (!b.HasValue) return a;
        return LerpAngle(a.Value, b.Value, t);
    }

    private WeatherState CloneState(WeatherState state)
    {
        return new WeatherState
        {
            ObservationTime = state.ObservationTime,
            StationId = state.StationId,
            RawMetar = state.RawMetar,
            Taf = state.Taf,
            Latitude = state.Latitude,
            Longitude = state.Longitude,
            TemperatureCelsius = state.TemperatureCelsius,
            DewpointCelsius = state.DewpointCelsius,
            PressureHpa = state.PressureHpa,
            AltimeterHpa = state.AltimeterHpa,
            VisibilityMeters = state.VisibilityMeters,
            FlightCategory = state.FlightCategory,
            WindDirectionDegrees = state.WindDirectionDegrees,
            WindSpeedKnots = state.WindSpeedKnots,
            WindGustKnots = state.WindGustKnots,
            GustDirectionDegrees = state.GustDirectionDegrees,
            CloudLayers = new List<CloudLayer>(state.CloudLayers),
            WindsAloft = CloneWindLayers(state.WindsAloft),
            Hazards = new List<WeatherHazard>(state.Hazards),
            StormCells = new List<StormCell>(state.StormCells),
            Precipitation = state.Precipitation,
            PrecipitationRate = state.PrecipitationRate,
            HumidityPercent = state.HumidityPercent,
            FreezingLevelFeet = state.FreezingLevelFeet,
            CeilingFeet = state.CeilingFeet,
            IcingIndex = state.IcingIndex,
            TurbulenceIndex = state.TurbulenceIndex,
            IcingLayers = new List<IcingLayer>(state.IcingLayers),
            TurbulenceLayers = new List<TurbulenceLayer>(state.TurbulenceLayers),
            ThunderstormIntensity = state.ThunderstormIntensity,
            AerosolDensity = state.AerosolDensity,
            SourceModelName = state.SourceModelName,
            DataAgeMinutes = state.DataAgeMinutes,
            ConvectiveAvailablePotentialEnergy = state.ConvectiveAvailablePotentialEnergy,
            LiftedIndex = state.LiftedIndex
        };
    }
}
