using System.Diagnostics;
using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;

namespace SkyWeave.SimBridge;

public enum DEFINITIONS
{
    AircraftPosition = 0,
    WeatherReadback = 1
}

public enum REQUESTS
{
    AircraftPosition = 0,
    WeatherReadback = 1
}

public enum CommBusEvents
{
    BridgeAck = 0,
    BridgeHeartbeat = 1
}

public enum EVENTS
{
    SimRate = 100
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
    public double VisibilityMeters;

    // MSFS exposes no supported sky-cover SimVar. A computed property adds no
    // bytes to the five-double native layout; unknown must never mean clear.
    public readonly double CloudCoverageOktas => double.NaN;
}

/// <summary>A message received from the in-sim HTML/JS bridge over CommBus.</summary>
public sealed record BridgeAckData(uint EventID, string? Data);

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
    private readonly object _simConnectCallLock = new();

    private SimConnect? _simConnect;
    private Thread? _pumpThread;
    private volatile bool _pumpRunning;

    private bool _isConnected;
    private bool _disposed;
    private bool _definitionsRegistered;

    private AircraftPositionData _lastPosition;
    private bool _hasPosition;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? LogMessage;
    public event EventHandler<AircraftPositionData>? PositionUpdated;
    public event EventHandler<AmbientWeatherData>? WeatherReadbackReceived;
    public event EventHandler<BridgeAckData>? BridgeAckReceived;
    public event EventHandler<double>? SimulationRateChanged;

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
        try
        {
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                if (MSFS_PROCESS_NAMES.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Process enumeration failure should not block the attempt.
        }
        return false;
    }

    /// <summary>
    /// True when the installed managed SimConnect SDK exposes the CommBus
    /// call used to reach the in-sim HTML/JS weather bridge. The local SDK
    /// build can lag the MSFS 2024 docs, so this is probed at runtime.
    /// </summary>
    public static bool IsCommBusSupported()
    {
        try
        {
            return typeof(SimConnect).GetMethod(
                "CallCommBusEvent",
                BindingFlags.Instance | BindingFlags.Public) != null;
        }
        catch
        {
            return false;
        }
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
            simConnect.OnRecvEvent += OnSimConnectEvent;

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
    /// Loads a weather preset by name. The preset WPR file must already
    /// exist in the MSFS weather presets folder. This uses the deprecated
    /// SimConnect_WeatherSetModeTheme API — it may or may not work in
    /// MSFS 2024. Returns true if the call was made without error.
    /// </summary>
    public bool SetWeatherTheme(string presetName)
    {
        var simConnect = CurrentSimConnect;
        if (simConnect == null || string.IsNullOrWhiteSpace(presetName))
            return false;

        try
        {
            LogMessage?.Invoke(this, $"WeatherSetModeTheme(\"{presetName}\")");
            simConnect.WeatherSetModeTheme(presetName);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"WeatherSetModeTheme failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sends a versioned weather command to the optional in-sim HTML/JS bridge.
    /// This bypasses the managed wrapper's CallCommBusEvent, whose internal
    /// MarshalToPtr walks uninitialized heap past the ANSI string terminator
    /// and computes a garbage buffer size (AccessViolation, seen live
    /// 2026-08-19). The native export is called directly with an exact
    /// UTF-8 buffer. A true return only means the message was queued; the
    /// bridge ack and the readback remain authoritative.
    /// </summary>
    public bool SendWeatherBridgeMessage(string message)
    {
        var simConnect = CurrentSimConnect;
        if (simConnect == null || string.IsNullOrWhiteSpace(message))
            return false;

        try
        {
            if (!IsCommBusSupported())
            {
                LogMessage?.Invoke(this,
                    "Weather bridge unavailable — installed SimConnect SDK has no CallCommBusEvent");
                return false;
            }

            var handle = GetSimConnectHandle(simConnect);
            if (handle == IntPtr.Zero)
            {
                ErrorOccurred?.Invoke(this, "Weather bridge command failed — no SimConnect handle");
                return false;
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            var buffer = Marshal.AllocHGlobal(bytes.Length + 1);
            try
            {
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
                Marshal.WriteByte(buffer, bytes.Length, 0);

                lock (_simConnectCallLock)
                {
                    var result = NativeCallCommBusEvent(
                        handle,
                        WeatherBridgeProtocol.ApplyEventName,
                        (uint)SIMCONNECT_COMM_BUS_BROADCAST_TO.JS,
                        (uint)bytes.Length + 1,
                        buffer);
                    if (result < 0)
                    {
                        ErrorOccurred?.Invoke(this,
                            $"Weather bridge command failed — SimConnect_CallCommBusEvent HRESULT 0x{result:X8}");
                        return false;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            LogMessage?.Invoke(this, "Weather bridge command queued for JS");
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Weather bridge command failed: {ex.Message}");
            return false;
        }
    }

    [DllImport("SimConnect.dll", EntryPoint = "SimConnect_CallCommBusEvent", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int NativeCallCommBusEvent(
        IntPtr hSimConnect,
        string eventName,
        uint broadcastTo,
        uint bufferSize,
        IntPtr data);

    private static IntPtr GetSimConnectHandle(SimConnect simConnect)
    {
        try
        {
            var field = typeof(SimConnect).GetField(
                "hSimConnect",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(simConnect) is IntPtr handle ? handle : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
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

        // Ambient weather readback
        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "AMBIENT WIND DIRECTION", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "AMBIENT WIND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "AMBIENT TEMPERATURE", "celsius", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "SEA LEVEL PRESSURE", "millibars", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simConnect.AddToDataDefinition(DEFINITIONS.WeatherReadback, "AMBIENT VISIBILITY", "meters", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
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
        TrySubscribeCommBusAck(sender);
        TrySubscribeSimRate(sender);
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void TrySubscribeSimRate(SimConnect sender)
    {
        try
        {
            sender.SubscribeToSystemEvent(EVENTS.SimRate, "SimRate");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"SimRate subscription unavailable: {ex.Message}");
        }
    }

    private void OnSimConnectEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
    {
        if (data.uEventID == (uint)EVENTS.SimRate)
        {
            var rate = (double)data.dwData;
            if (rate >= 0.25 && rate <= 128.0)
            {
                SimulationRateChanged?.Invoke(this, rate);
            }
        }
    }

    /// <summary>
    /// Subscribes to the bridge acknowledgement event so the desktop learns
    /// whether the in-sim JS actually accepted the weather command. Requires
    /// the SDK 1.6.4+ CommBus API; older SDKs degrade gracefully.
    /// </summary>
    private void TrySubscribeCommBusAck(SimConnect sender)
    {
        try
        {
            if (!IsCommBusSupported())
            {
                LogMessage?.Invoke(this,
                    "CommBus ack subscription unavailable — installed SimConnect SDK has no CommBus support");
                return;
            }

            sender.OnRecvCommBus += OnSimConnectCommBus;
            sender.SubscribeToCommBusEvent(
                CommBusEvents.BridgeAck,
                WeatherBridgeProtocol.AcknowledgeEventName);
            sender.SubscribeToCommBusEvent(
                CommBusEvents.BridgeHeartbeat,
                WeatherBridgeProtocol.HeartbeatEventName);

            LogMessage?.Invoke(this,
                $"CommBus ack/heartbeat subscription active");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"CommBus ack subscription failed: {ex.Message}");
        }
    }

    private void OnSimConnectCommBus(SimConnect sender, SIMCONNECT_RECV_COMM_BUS data)
    {
        try
        {
            if (data.uEventID == (uint)CommBusEvents.BridgeAck)
            {
                BridgeAckReceived?.Invoke(this, new BridgeAckData(data.uEventID, data.rgData));
            }
            else if (data.uEventID == (uint)CommBusEvents.BridgeHeartbeat)
            {
                try 
                {
                    var doc = System.Text.Json.JsonDocument.Parse(data.rgData);
                    if (doc.RootElement.TryGetProperty("version", out var versionElement))
                    {
                        if (versionElement.GetInt32() != WeatherBridgeProtocol.Version)
                        {
                            LogMessage?.Invoke(this, $"WARNING: JS bridge protocol mismatch. C# v{WeatherBridgeProtocol.Version}, JS v{versionElement.GetInt32()}");
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"CommBus message parse error: {ex.Message}");
        }
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
            1 => "error",
            2 => "size mismatch",
            3 => "unrecognized ID",
            7 => "name not recognized",
            13 => "weather invalid port",
            14 => "weather invalid METAR",
            15 => "weather unable to get observation",
            16 => "weather unable to create station",
            17 => "weather unable to remove station",
            18 => "invalid data type",
            19 => "invalid data size",
            20 => "data error",
            21 => "invalid array (usually a write to read-only SimVars)",
            27 => "invalid enum",
            31 => "out of bounds",
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
