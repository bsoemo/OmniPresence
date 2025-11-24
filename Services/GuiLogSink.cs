using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace OmniPresence.Services;

/// <summary>
/// Custom Serilog sink that captures log events for display in the GUI
/// </summary>
public class GuiLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<GuiLogEntry> _logQueue = new();
    private readonly int _maxLogs;
    private int _logCount = 0;

    public event EventHandler<GuiLogEntry>? LogAdded;

    public GuiLogSink(int maxLogs = 500)
    {
        _maxLogs = maxLogs;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) return;

        // Extract simple error message if exception present
        var errorMessage = logEvent.Exception?.Message ?? string.Empty;
        
        var entry = new GuiLogEntry
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level.ToString().Substring(0, 3).ToUpper(), // INF, WRN, ERR, etc.
            Message = logEvent.RenderMessage(),
            ErrorMessage = errorMessage,
            Exception = logEvent.Exception
        };

        _logQueue.Enqueue(entry);
        _logCount++;

        // Keep queue size manageable - remove oldest if too many
        while (_logQueue.Count > _maxLogs && _logQueue.TryDequeue(out _))
        {
            // Remove oldest entry
        }

        // Notify subscribers
        LogAdded?.Invoke(this, entry);
    }

    public List<GuiLogEntry> GetAllLogs()
    {
        return _logQueue.ToList();
    }

    public void Clear()
    {
        while (_logQueue.TryDequeue(out _))
        {
            // Clear all
        }
    }
}

public class GuiLogEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string Level { get; set; } = "INF";
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }

    public override string ToString()
    {
        var timeStr = Timestamp.ToString("HH:mm:ss");
        var msg = Message;
        
        // Include error message if present (without full stack trace)
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            msg = $"{msg} | Error: {ErrorMessage}";
        }

        return $"[{timeStr} {Level}] {msg}";
    }
}
