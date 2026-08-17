using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SkyWeave.SimBridge;

public class SimConnectManager : IDisposable
{
    private const string MSFS_PROCESS_NAME = "FlightSimulator2024";
    private const string APP_NAME = "SkyWeave";
    private const uint SIMCONNECT_OBJECT_ID_USER = 0x40000000;

    private bool _isConnected;
    private bool _disposed;
    private bool _simConnectAvailable;
    private Timer? _positionPollTimer;
    private (double Lat, double Lon, double Alt) _lastPosition;
    private object? _simConnectInstance;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<(double Latitude, double Longitude, double AltitudeFeet)>? PositionUpdated;

    public bool IsConnected => _isConnected;
    public TimeSpan PositionPollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public bool IsSimConnectAvailable => _simConnectAvailable;

    public bool IsSimRunning()
    {
        return Process.GetProcessesByName(MSFS_PROCESS_NAME).Length > 0;
    }

    public bool Connect()
    {
        try
        {
            if (!IsSimRunning())
            {
                ErrorOccurred?.Invoke(this, "MSFS 2024 is not running. Please start the simulator first.");
                return false;
            }

            _isConnected = true;
            _lastPosition = (40.6413, -73.7781, 0);

            try
            {
                if (!OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException("SimConnect is only available on Windows.");

                var simConnectType = Type.GetTypeFromProgID("Microsoft.FlightSimulator.SimConnect.SimConnect");
                if (simConnectType == null)
                {
                    ErrorOccurred?.Invoke(this, "SimConnect COM type not registered. Running in simulation mode.");
                    Connected?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                _simConnectInstance = Activator.CreateInstance(simConnectType);
                if (_simConnectInstance == null)
                {
                    ErrorOccurred?.Invoke(this, "Failed to create SimConnect instance. Running in simulation mode.");
                    Connected?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                var onRecvOpenEvent = simConnectType.GetEvent("OnRecvOpen");
                var onRecvQuitEvent = simConnectType.GetEvent("OnRecvQuit");
                var onRecvSimObjectDataEvent = simConnectType.GetEvent("OnRecvSimobjectData");
                var onRecvExceptionEvent = simConnectType.GetEvent("OnRecvException");

                onRecvOpenEvent?.AddEventHandler(_simConnectInstance, (EventHandler<dynamic>)((s, e) => OnSimConnectOpen(s, e)));
                onRecvQuitEvent?.AddEventHandler(_simConnectInstance, (EventHandler<dynamic>)((s, e) => OnSimConnectQuit(s, e)));
                onRecvSimObjectDataEvent?.AddEventHandler(_simConnectInstance, (EventHandler<dynamic>)((s, e) => OnSimConnectSimObjectData(s, e)));
                onRecvExceptionEvent?.AddEventHandler(_simConnectInstance, (EventHandler<dynamic>)((s, e) => OnSimConnectException(s, e)));

                var addToDataDefinitionMethod = simConnectType.GetMethod("AddToDataDefinition", new[] { typeof(uint), typeof(string), typeof(string), typeof(uint), typeof(float), typeof(uint) });
                var requestDataOnSimObjectMethod = simConnectType.GetMethod("RequestDataOnSimObject", new[] { typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint) });

                if (addToDataDefinitionMethod != null)
                {
                    addToDataDefinitionMethod.Invoke(_simConnectInstance, new object[] { 0u, "Plane Latitude", "degrees", 3u, 0.0f, 0u });
                    addToDataDefinitionMethod.Invoke(_simConnectInstance, new object[] { 0u, "Plane Longitude", "degrees", 3u, 0.0f, 0u });
                    addToDataDefinitionMethod.Invoke(_simConnectInstance, new object[] { 0u, "Plane Altitude", "feet", 3u, 0.0f, 0u });
                }

                if (requestDataOnSimObjectMethod != null)
                {
                    requestDataOnSimObjectMethod.Invoke(_simConnectInstance, new object[] { 0u, 0u, SIMCONNECT_OBJECT_ID_USER, 1u, 0u, 0u, 0u, 0u });
                }

                _simConnectAvailable = true;
                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"SimConnect init failed: {ex.Message}. Running in simulation mode.");
                _simConnectInstance = null;
                _simConnectAvailable = false;
                Connected?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}. Running in simulation mode.");
            _isConnected = true;
            _lastPosition = (40.6413, -73.7781, 0);
            Connected?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }

    public void Disconnect()
    {
        if (_isConnected || _simConnectInstance != null)
        {
            _positionPollTimer?.Dispose();
            _positionPollTimer = null;

            if (_simConnectInstance != null)
            {
                try
                {
                    var simConnectType = _simConnectInstance.GetType();
                    var disposeMethod = simConnectType.GetMethod("Dispose");
                    disposeMethod?.Invoke(_simConnectInstance, null);
                }
                catch
                {
                    // Ignore cleanup errors
                }
                _simConnectInstance = null;
            }

            _isConnected = false;
            _simConnectAvailable = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    public (double Latitude, double Longitude, double AltitudeFeet) GetAircraftPosition()
    {
        if (!_isConnected)
            return (0, 0, 0);

        return _lastPosition;
    }

    public void SetWeather(string wprXml)
    {
        if (!_isConnected || _simConnectInstance == null)
            return;

        try
        {
            var simConnectType = _simConnectInstance.GetType();
            var weatherSetModeCustomMethod = simConnectType.GetMethod("WeatherSetModeCustom");
            weatherSetModeCustomMethod?.Invoke(_simConnectInstance, null);

            var setDataMethod = simConnectType.GetMethod("SetDataOnSimObject", new[] { typeof(uint), typeof(uint), typeof(uint), typeof(byte[]) });
            if (setDataMethod != null)
            {
                var data = System.Text.Encoding.UTF8.GetBytes(wprXml);
                setDataMethod.Invoke(_simConnectInstance, new object[] { 1u, SIMCONNECT_OBJECT_ID_USER, 0u, data });
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Weather injection error: {ex.Message}");
        }
    }

    private void OnSimConnectOpen(object? sender, dynamic e)
    {
        _positionPollTimer = new Timer(
            _ => PollPosition(),
            null,
            TimeSpan.Zero,
            PositionPollInterval);

        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void OnSimConnectQuit(object? sender, dynamic e)
    {
        Disconnect();
    }

    private void OnSimConnectSimObjectData(object? sender, dynamic e)
    {
        try
        {
            if (e.dwRequestID == 0 && e.dwData.Length > 0)
            {
                var pos = e.dwData[0];
                _lastPosition = (pos.Latitude, pos.Longitude, pos.Altitude);
                PositionUpdated?.Invoke(this, _lastPosition);
            }
        }
        catch
        {
            // Keep last known position on parse failure
        }
    }

    private void OnSimConnectException(object? sender, dynamic e)
    {
        ErrorOccurred?.Invoke(this, $"SimConnect exception: {e.dwException} (ID: {e.dwIndex})");
    }

    private void PollPosition()
    {
        if (!_isConnected || _simConnectInstance == null) return;

        try
        {
            var simConnectType = _simConnectInstance.GetType();
            var requestMethod = simConnectType.GetMethod("RequestDataOnSimObject", new[] { typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint) });
            requestMethod?.Invoke(_simConnectInstance, new object[] { 0u, 0u, SIMCONNECT_OBJECT_ID_USER, 1u, 0u, 0u, 0u, 0u });
        }
        catch
        {
            // Polling errors are non-fatal
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
