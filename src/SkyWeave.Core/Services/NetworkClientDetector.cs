using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SkyWeave.Core.Services;

/// <summary>
/// Detects if an online air traffic control pilot client (e.g. vPilot, xPilot, Altitude, Swift)
/// is currently active on the system and notifies subscribers of status changes.
/// </summary>
public class NetworkClientDetector : IDisposable
{
    public static readonly IReadOnlyList<string> KnownClients = new[]
    {
        "vPilot",
        "xPilot",
        "Altitude",
        "Swift",
        "vatsim",
        "ivao"
    };

    private readonly Func<IEnumerable<string>> _processProvider;
    private readonly Timer _pollTimer;
    private readonly object _lock = new();

    private bool _lastIsRunning;
    private string? _lastClientName;
    private bool _disposed;
    private int _isChecking;

    /// <summary>
    /// Event triggered when the online client running status or active client changes.
    /// (bool isRunning, string? clientName)
    /// </summary>
    public event Action<bool, string?>? OnlineClientStatusChanged;

    public NetworkClientDetector()
        : this(GetSystemProcessNames, null)
    {
    }

    public NetworkClientDetector(TimeSpan pollInterval)
        : this(GetSystemProcessNames, pollInterval)
    {
    }

    public NetworkClientDetector(Func<IEnumerable<string>> processProvider, TimeSpan? pollInterval = null)
    {
        _processProvider = processProvider ?? throw new ArgumentNullException(nameof(processProvider));
        _pollTimer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);

        if (pollInterval.HasValue && pollInterval.Value > TimeSpan.Zero)
        {
            Start(pollInterval.Value);
        }
    }

    /// <summary>
    /// Starts periodic polling with the specified interval.
    /// </summary>
    public void Start(TimeSpan interval)
    {
        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NetworkClientDetector));

            _pollTimer.Change(interval, interval);
        }
    }

    /// <summary>
    /// Stops periodic polling.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Checks running processes and returns whether any known online client is currently running.
    /// </summary>
    public bool IsOnlineClientRunning()
    {
        var (isRunning, _) = CheckStatus();
        return isRunning;
    }

    /// <summary>
    /// Checks running processes and returns the canonical name of the detected online client, or null if none is running.
    /// </summary>
    public string? GetDetectedClientName()
    {
        var (_, clientName) = CheckStatus();
        return clientName;
    }

    /// <summary>
    /// Explicitly triggers an immediate check of running processes and returns current status.
    /// Fires OnlineClientStatusChanged if the status has transitioned.
    /// </summary>
    public (bool IsRunning, string? ClientName) CheckStatus()
    {
        lock (_lock)
        {
            if (_disposed)
                return (_lastIsRunning, _lastClientName);

            string? detectedClient = null;
            try
            {
                var processes = _processProvider();
                detectedClient = MatchClient(processes);
            }
            catch
            {
                // Isolate failure: do not propagate exception out of status check
                return (_lastIsRunning, _lastClientName);
            }

            bool isRunning = detectedClient != null;
            bool statusChanged = isRunning != _lastIsRunning ||
                !string.Equals(detectedClient, _lastClientName, StringComparison.OrdinalIgnoreCase);

            if (statusChanged)
            {
                _lastIsRunning = isRunning;
                _lastClientName = detectedClient;
                try
                {
                    OnlineClientStatusChanged?.Invoke(isRunning, detectedClient);
                }
                catch
                {
                    // Subscriber exceptions do not break the detector
                }
            }

            return (isRunning, detectedClient);
        }
    }

    /// <summary>
    /// Helper method for periodic check without returning values.
    /// </summary>
    public void CheckNow() => CheckStatus();

    private void OnTimerTick(object? state)
    {
        if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
            return;

        try
        {
            CheckStatus();
        }
        catch
        {
            // Defensive: ensure background timer never crashes
        }
        finally
        {
            Interlocked.Exchange(ref _isChecking, 0);
        }
    }

    private static string? MatchClient(IEnumerable<string> processNames)
    {
        var namesList = processNames as IList<string> ?? processNames.ToList();

        // 1. Exact match (case-insensitive) against known client names
        foreach (var known in KnownClients)
        {
            foreach (var rawName in namesList)
            {
                var name = NormalizeProcessName(rawName);
                if (string.Equals(name, known, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }
        }

        // 2. Prefix or substring match (e.g. "swift-gui", "vpilot_client", "altitude.exe")
        foreach (var known in KnownClients)
        {
            foreach (var rawName in namesList)
            {
                var name = NormalizeProcessName(rawName);
                if (name.StartsWith(known, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(known, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }
        }

        return null;
    }

    private static string NormalizeProcessName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        var trimmed = rawName.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];

        return trimmed;
    }

    private static IEnumerable<string> GetSystemProcessNames()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return Array.Empty<string>();
        }

        var names = new List<string>(processes.Length);
        foreach (var p in processes)
        {
            try
            {
                names.Add(p.ProcessName);
            }
            catch
            {
                // Process terminated or access denied
            }
            finally
            {
                try
                {
                    p.Dispose();
                }
                catch
                {
                }
            }
        }

        return names;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _pollTimer.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
