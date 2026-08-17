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

    public bool IsTransitioning
    {
        get
        {
            lock (_lock)
            {
                return _target != null && 
                       (DateTime.UtcNow - _transitionStart) < _transitionDuration;
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
            var t = Math.Min(1.0, elapsed.TotalSeconds / _transitionDuration.TotalSeconds);
            t = SmoothStep(t);

            return Interpolate(_current, _target, t);
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

    private WeatherState Interpolate(WeatherState from, WeatherState to, double t)
    {
        var result = new WeatherState
        {
            ObservationTime = to.ObservationTime,
            StationId = to.StationId,
            Latitude = to.Latitude,
            Longitude = to.Longitude,
            FlightCategory = to.FlightCategory,
            Precipitation = to.Precipitation,

            TemperatureCelsius = Lerp(from.TemperatureCelsius, to.TemperatureCelsius, t),
            DewpointCelsius = Lerp(from.DewpointCelsius, to.DewpointCelsius, t),
            PressureHpa = Lerp(from.PressureHpa, to.PressureHpa, t),
            AltimeterHpa = Lerp(from.AltimeterHpa, to.AltimeterHpa, t),
            VisibilityMeters = Lerp(from.VisibilityMeters, to.VisibilityMeters, t),
            WindDirectionDegrees = LerpAngle(from.WindDirectionDegrees, to.WindDirectionDegrees, t),
            WindSpeedKnots = Lerp(from.WindSpeedKnots, to.WindSpeedKnots, t),
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

            CloudLayers = to.CloudLayers,
            WindsAloft = to.WindsAloft,
            Hazards = to.Hazards,
            StormCells = to.StormCells,
            IcingLayers = to.IcingLayers,
            TurbulenceLayers = to.TurbulenceLayers
        };

        return result;
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
            WindsAloft = new List<WindLayer>(state.WindsAloft),
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
            AerosolDensity = state.AerosolDensity
        };
    }
}
