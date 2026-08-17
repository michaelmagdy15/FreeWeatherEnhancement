using System.Diagnostics;
using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace SkyWeave.SimBridge;

public enum DEFINITIONS
{
    AircraftPosition = 0,
    AmbientWeather = 1,
    WeatherReadback = 2
}

public enum REQUESTS
{
    AircraftPosition = 0,
    WeatherReadback = 1
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AircraftPositionData
{
    public double Latitude;
    public double Longitude;
    public double AltitudeFeet;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AmbientWeatherData
{
    public double WindDirectionDegrees;
    public double WindSpeedKnots;
    public double TemperatureCelsius;
    public double SeaLevelPressureHpa;
}

/// <summary>
/// Real SimConnect bridge using the typed managed SDK assembly
/// (Microsoft.FlightSimulator.SimConnect.dll). Connection is only "connected"
/// after the sim acknowledges via OnRecvOpen. Position is only available from
/// real sim readback — there is no default position.
/// </summary>
public class SimConnectManager : IDisposable
{
    private const string APP_NAME = "SkyWeave";
    private const uint WM_USER_SIMCONNECT = 0x0402;

    // MSFS 2024 and 2020 both run as "FlightSimulator"; check both names (audit C5).
    private static readonly string[] MSFS_PROCESS_NAMES = { "FlightSimulator", "FlightSimulator2024" };

    private readonly object _stateLock = new();

    private SimConnect? _simConnect;
    private Thread? _pumpThread;
    private volatile bool _pumpRunning;

    private bool _isConnected;
    private bool _disposed;
    private bool _definitionsRegistered;
    private bool _customWeatherModeSet;

    private AircraftPositionData _lastPosition;
    private bool _hasPosition;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<AircraftPositionData>? PositionUpdated;
    public event EventHandler<AmbientWeatherData>? WeatherReadbackReceived;

    /// <summary>True only after the sim acknowledged the connection (OnRecvOpen).</summary>
    public bool IsConnected
    {
        get { lock (_stateLock) return _isConnected; }
    }

    /// <summary>True only after a real position fix arrived from the sim. Never defaults.</summary>
    public bool HasPosition
    {
        get { lock (_stateLock) return _hasPosition; }
    }

    public bool IsSimRunning()
    {
        foreach (var name in MSFS_PROCESS_NAMES)
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0)
                    return true;
            }
            catch
            {
                // Process enumeration failure should not block the attempt.
            }
        }
        return false;
    }

    /// <summary>
    /// Starts a connection attempt against the running sim.
    /// Returns true when the attempt is underway — NOT when connected.
    /// Connection itself is only established when the sim answers OnRecvOpen,
    /// which raises the <see cref="Connected"/> event. This method never
    /// reports success on failure paths (audit C2).
    /// </summary>
    public bool Connect()
    {
        lock (_stateLock)
        {
            if (_isConnected)
                return true;
            if (_simConnect != null)
                return true; // attempt already in progress
        }

        if (!IsSimRunning())
        {
            ErrorOccurred?.Invoke(this, "MSFS is not running. Start the simulator, then connect.");
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            ErrorOccurred?.Invoke(this, "SimConnect is only available on Windows.");
            return false;
        }

        try
        {
            var simConnect = new SimConnect(APP_NAME, IntPtr.Zero, WM_USER_SIMCONNECT, null, 0);

            simConnect.OnRecvOpen += OnSimConnectOpen;
            simConnect.OnRecvQuit += OnSimConnectQuit;
            simConnect.OnRecvException += OnSimConnectException;
            simConnect.OnRecvSimobjectData += OnSimConnectSimObjectData;

            lock (_stateLock)
            {
                _simConnect = simConnect;
            }

            StartPumpThread();
            return true;
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _simConnect = null;
                _isConnected = false;
            }
            ErrorOccurred?.Invoke(this, $"SimConnect connection failed: {ex.Message}");
            return false;
        }
    }

    public void Disconnect()
    {
        Teardown("Disconnected by user");
    }

    /// <summary>
    /// Returns the last real position fix from the sim, or null when none has
    /// arrived yet. There is no default position (audit C4).
    /// </summary>
    public AircraftPositionData? GetAircraftPosition()
    {
        lock (_stateLock)
        {
            return _hasPosition ? _lastPosition : null;
        }
    }

    /// <summary>
    /// Writes surface weather (wind, temperature, QNH) to the sim in CUSTOM
    /// weather mode. Returns true when the write was issued to SimConnect
    /// without error — verification is asynchronous via readback
    /// (see <see cref="RequestWeatherReadback"/> / <see cref="WeatherReadbackReceived"/>).
    /// </summary>
    public bool SetAmbientWeather(AmbientWeatherData weather)
    {
        var simConnect = CurrentSimConnect;
        if (simConnect == null)
            return false;

        try
        {
            if (!_customWeatherModeSet)
            {
                simConnect.WeatherSetModeCustom();
                _customWeatherModeSet = true;
            }

            simConnect.SetDataOnSimObject(
                DEFINITIONS.AmbientWeather,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_DATA_SET_FLAG.DEFAULT,
                weather);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Weather injection error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Requests a one-shot readback of the ambient weather sim vars.</summary>
    public bool RequestWeatherReadback()
    {
        var simConnect = CurrentSimConnect;
        if (simConnect == null)
            return false;

        try
        {
            simConnect.RequestDataOnSimObject(
                REQUESTS.WeatherReadback,
                DEFINITIONS.WeatherReadback,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.ONCE,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Weather readback request failed: {ex.Message}");
            return false;
        }
    }

    private SimConnect? CurrentSimConnect
    {
        get { lock (_stateLock) return _isConnected ? _simConnect : null; }
    }

    private void StartPumpThread()
    {
        _pumpRunning = true;
        _pumpThread = new Thread(PumpLoop)
        {
            IsBackground = true,
            Name = "SimConnectPump"
        };
        _pumpThread.Start();
    }

    private void PumpLoop()
    {
        while (_pumpRunning)
        {
            SimConnect? simConnect;
            lock (_stateLock)
            {
                simConnect = _simConnect;
            }

            if (simConnect == null)
                return;

            try
            {
                simConnect.ReceiveMessage();
            }
            catch (Exception ex)
            {
                // The pipe broke — the sim is gone or the connection died.
                Teardown($"Connection to sim lost: {ex.Message}");
                return;
            }

            Thread.Sleep(50);
        }
    }

    private void RegisterDefinitionsAndRequests(SimConnect simConnect)
    {
        if (_definitionsRegistered)
            return;

        // Position (read)
        simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.AircraftPosition, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.RegisterDataDefineStruct<AircraftPositionData>(DEFINITIONS.AircraftPosition);

        // Ambient weather (write + readback of the same vars)
        simConnect.AddToDataDefinition(DEFINITIONS.AmbientWeather, "AMBIENT WIND DIRECTION", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.AmbientWeather, "AMBIENT WIND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.AmbientWeather, "AMBIENT TEMPERATURE", "celsius", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.AmbientWeather, "SEA LEVEL PRESSURE", "millibars", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.RegisterDataDefineStruct<AmbientWeatherData>(DEFINITIONS.AmbientWeather);

        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "AMBIENT WIND DIRECTION", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "AMBIENT WIND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "AMBIENT TEMPERATURE", "celsius", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "SEA LEVEL PRESSURE", "millibars", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.RegisterDataDefineStruct<AmbientWeatherData>(DEFINITIONS.WeatherReadback);

        // Periodic position: 1 Hz while connected
        simConnect.RequestDataOnSimObject(
            REQUESTS.AircraftPosition,
            DEFINITIONS.AircraftPosition,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0, 0, 0);

        _definitionsRegistered = true;
    }

    private void OnSimConnectOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        lock (_stateLock)
        {
            _isConnected = true;
        }

        RegisterDefinitionsAndRequests(sender);
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void OnSimConnectQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Teardown("Simulator closed the connection (sim quit or weather system reset)");
    }

    private void OnSimConnectSimObjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwData == null || data.dwData.Length == 0)
            return;

        try
        {
            if (data.dwRequestID == (uint)REQUESTS.AircraftPosition)
            {
                var pos = (AircraftPositionData)data.dwData[0]!;
                lock (_stateLock)
                {
                    _lastPosition = pos;
                    _hasPosition = true;
                }
                PositionUpdated?.Invoke(this, pos);
            }
            else if (data.dwRequestID == (uint)REQUESTS.WeatherReadback)
            {
                var readback = (AmbientWeatherData)data.dwData[0]!;
                WeatherReadbackReceived?.Invoke(this, readback);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Sim data parse error: {ex.Message}");
        }
    }

    private void OnSimConnectException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        var description = DescribeException(data.dwException);
        ErrorOccurred?.Invoke(this, $"SimConnect exception ({data.dwException}): {description}");
    }

    private static string DescribeException(uint code)
    {
        return code switch
        {
            1 => "unrecognized ID",
            2 => "unrecognized message",
            3 => "unrecognized event",
            5 => "unrecognized data definition",
            8 => "unrecognized data request ID",
            10 => "unrecoverable error",
            30 => "weather init not complete",
            31 => "weather failed to load",
            32 => "weather invalid port",
            _ => "see SimConnect SDK documentation"
        };
    }

    private void Teardown(string reason)
    {
        lock (_stateLock)
        {
            if (!_isConnected && _simConnect == null)
                return;

            _pumpRunning = false;
            try { _simConnect?.Dispose(); } catch { /* disposal of a dead pipe */ }
            _simConnect = null;
            _isConnected = false;
            _hasPosition = false;
            _definitionsRegistered = false;
            _customWeatherModeSet = false;
        }

        Disconnected?.Invoke(this, EventArgs.Empty);
        ErrorOccurred?.Invoke(this, reason);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Teardown("Disposed");
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
