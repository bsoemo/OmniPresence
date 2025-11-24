using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.RegularExpressions;

namespace OmniPresence.Services;

public class TeamsLogMonitor : IStatusMonitor
{
    private readonly ILogger<TeamsLogMonitor> _logger;
    private readonly IConfigurationService _configService;
    private string _currentStatus = "Unknown";
    private bool _isInCall = false;
    private string? _lastReportedStatus;
    private bool? _lastReportedActivity;
    private bool _initialStatusSent = false;
    private CancellationTokenSource? _cancellationTokenSource;
    private long _lastReadPosition = 0;
    private string? _currentLogFile;

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;
    public event EventHandler<CallActivityChangedEventArgs>? CallActivityChanged;

    public string CurrentStatus => _currentStatus;
    public bool IsInCall => _isInCall;

    public TeamsLogMonitor(ILogger<TeamsLogMonitor> logger, IConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _logger.LogInformation("Teams log monitor initialized - PollingInterval: {PollingInterval}s, DebugMode: {DebugMode}", 
            _configService.TeamsPollingIntervalSeconds, _configService.TeamsDebugMode);

        // Read initial status from logs
        await ReadInitialStatusAsync();

        // Send initial status if we found one
        if (_initialStatusSent)
        {
            _logger.LogInformation("Initial status sent: {Status}", _currentStatus);
        }

        // Start the monitoring loop
        _ = MonitorLogFileAsync();

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cancellationTokenSource?.Cancel();
        _logger.LogInformation("Teams log monitor stopped");
        await Task.CompletedTask;
    }

    private async Task ReadInitialStatusAsync()
    {
        try
        {
            var logPath = GetTeamsLogDirectory();
            if (logPath == null)
            {
                _logger.LogWarning("Teams log directory not found - cannot read initial status");
                return;
            }

            _logger.LogInformation("Reading initial Teams status from logs... (Path: {LogPath})", logPath);

            var latestLogFile = GetLatestLogFile(logPath);
            if (latestLogFile == null)
            {
                _logger.LogWarning("No Teams log files found");
                return;
            }

            if (_configService.TeamsDebugMode)
            {
                _logger.LogInformation("[DEBUG] Using log file: {LogFile}", latestLogFile);
            }

            _logger.LogDebug("Reading entire log file: {LogFile}", Path.GetFileName(latestLogFile));
            var lines = await ReadLastLinesAsync(latestLogFile, int.MaxValue);
            
            if (lines.Count == 0)
            {
                _logger.LogWarning("Log file is empty or unreadable: {LogFile}", latestLogFile);
                return;
            }

            _logger.LogDebug("Read {LineCount} lines from log file", lines.Count);
            var update = ParseTeamsLog(lines);

            if (update != null)
            {
                _currentStatus = update.Status;
                _isInCall = update.IsInCall;
                _lastReportedStatus = update.Status;
                _lastReportedActivity = update.IsInCall;
                _initialStatusSent = true;

                _logger.LogInformation("Initial status detected: Status={Status}, Activity={Activity}",
                    _currentStatus, _isInCall ? "In a call" : "Not in a call");

                OnStatusChanged("Unknown", _currentStatus);
                OnCallActivityChanged(_isInCall);
            }
            else
            {
                _logger.LogWarning("Could not detect Teams status from log file - patterns may not match");
                _logger.LogDebug("First few log lines for debugging:");
                foreach (var line in lines.Take(10))
                {
                    _logger.LogDebug("  {LogLine}", line);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading initial Teams status");
        }
    }

    private async Task MonitorLogFileAsync()
        {
            try
            {
                int pollingIntervalMs = _configService.TeamsPollingIntervalSeconds * 1000;
                
                if (_configService.TeamsDebugMode)
                {
                    _logger.LogInformation("[DEBUG] Starting monitoring loop with {PollingInterval}ms interval", pollingIntervalMs);
                }
                
                while (!(_cancellationTokenSource?.Token.IsCancellationRequested ?? false))
                {
                    try
                    {
                        var logPath = GetTeamsLogDirectory();
                        if (logPath == null)
                        {
                            await Task.Delay(pollingIntervalMs);
                            continue;
                        }

                        var logFile = GetLatestLogFile(logPath);
                        if (logFile == null)
                        {
                            await Task.Delay(pollingIntervalMs);
                            continue;
                        }

                        _currentLogFile = Path.GetFileName(logFile);

                        var lines = await ReadNewLinesAsync(logFile);
                        if (lines.Count > 0)
                        {
                            if (_configService.TeamsDebugMode)
                            {
                                _logger.LogInformation("[DEBUG] Read {LineCount} new lines from log", lines.Count);
                            }
                            
                            var update = ParseTeamsLog(lines);
                            if (update != null)
                            {
                                // Check if status changed
                                if (update.Status != _lastReportedStatus)
                                {
                                    _logger.LogInformation("Status changed to {Status} (from {LogFile})",
                                        update.Status, _currentLogFile);
                                    _currentStatus = update.Status;
                                    _lastReportedStatus = update.Status;
                                    OnStatusChanged(_lastReportedStatus ?? "Unknown", update.Status);
                                }

                                // Check if activity changed
                                if (update.IsInCall != _lastReportedActivity)
                                {
                                    _logger.LogInformation("Call activity changed to {Activity} (from {LogFile})",
                                        update.IsInCall ? "In call" : "Not in call", _currentLogFile);
                                    _isInCall = update.IsInCall;
                                    _lastReportedActivity = update.IsInCall;
                                    OnCallActivityChanged(update.IsInCall);
                                }
                            }
                        }

                        await Task.Delay(pollingIntervalMs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in monitoring loop");
                        await Task.Delay(5000);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in Teams log monitor");
            }
        }

    private string? GetTeamsLogDirectory()
        {
            // Use auto-detect or custom path from configuration
            if (_configService.TeamsAutoDetectLogPath)
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var teamsLogPath = Path.Combine(localAppData, "Packages", "MSTeams_8wekyb3d8bbwe", "LocalCache", "Microsoft", "MSTeams", "Logs");

                if (Directory.Exists(teamsLogPath))
                {
                    if (_configService.TeamsDebugMode)
                    {
                        _logger.LogDebug("[DEBUG] Using auto-detected log path: {TeamsLogPath}", teamsLogPath);
                    }
                    return teamsLogPath;
                }

                _logger.LogWarning("Auto-detected Teams log directory does not exist: {TeamsLogPath}", teamsLogPath);
                return null;
            }
            else
            {
                // Use custom path from configuration
                var customPath = _configService.TeamsLogPath;
                if (Directory.Exists(customPath))
                {
                    if (_configService.TeamsDebugMode)
                    {
                        _logger.LogDebug("[DEBUG] Using custom log path: {TeamsLogPath}", customPath);
                    }
                    return customPath;
                }

                _logger.LogWarning("Custom Teams log directory does not exist: {TeamsLogPath}", customPath);
                return null;
            }
        }

    private string? GetLatestLogFile(string logDirectory)
        {
            try
            {
                var logFiles = Directory.GetFiles(logDirectory, "*.log");

                if (logFiles.Length == 0)
                {
                    _logger.LogWarning("No .log files found in Teams log directory: {LogDirectory}", logDirectory);
                    return null;
                }

                var latestFile = logFiles
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();

                return latestFile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest log file from {LogDirectory}", logDirectory);
                return null;
            }
        }

    private async Task<List<string>> ReadLastLinesAsync(string filePath, int lineCount)
    {
        var lines = new List<string>();
        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = new StreamReader(fileStream))
                {
                    var allLines = new List<string>();
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        allLines.Add(line);
                    }

                    lines = allLines.TakeLast(lineCount).ToList();
                    _lastReadPosition = fileStream.Position;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading log file {FilePath}", filePath);
        }

        return lines;
    }

    private async Task<List<string>> ReadNewLinesAsync(string filePath)
    {
        var lines = new List<string>();
        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fileStream.Seek(_lastReadPosition, SeekOrigin.Begin);

                using (var reader = new StreamReader(fileStream))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        lines.Add(line);
                    }

                    _lastReadPosition = fileStream.Position;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading new lines from log file {FilePath}", filePath);
        }

        return lines;
    }

    private TeamsStatusUpdate? ParseTeamsLog(List<string> lines)
    {
        if (_configService.TeamsDebugMode)
        {
            _logger.LogDebug("[DEBUG] Parsing {LineCount} log lines for Teams status", lines.Count);
        }

        // Teams log patterns - updated for actual Teams log format
        var statusPatterns = new[]
        {
            (@"availability:\s*(?<status>\w+)", "availability"),
            (@"status\s*(?<status>Busy|Available|Away|Offline|DoNotDisturb|DND)", "status keyword"),
            (@"MsTeamsStatus set to (?<status>\w+)", "MsTeamsStatus"),
            (@"statusSetBy.*?status=(?<status>\w+)", "statusSetBy"),
        };

        var callPatterns = new[]
        {
            (@"\[CallState\].*?(?<state>Started|Ended)", "CallState"),
            (@"Call (?<state>started|ended)", "CallActivity"),
            (@"isInCall[:\s]*(?<state>true|false|True|False)", "isInCall"),
        };

        string? status = null;
        bool? isInCall = null;

        // Search for status and call state from most recent lines
        foreach (var line in lines.AsEnumerable().Reverse())
        {
            if (status == null)
            {
                foreach (var (pattern, patternName) in statusPatterns)
                {
                    var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        status = match.Groups["status"].Value;
                        if (_configService.TeamsDebugMode)
                        {
                            _logger.LogDebug("[DEBUG] Found status using pattern '{PatternName}': {Status}", patternName, status);
                            _logger.LogDebug("[DEBUG] Matched line: {LogLine}", line);
                        }
                        else
                        {
                            _logger.LogDebug("Found status using pattern '{PatternName}': {Status}", patternName, status);
                        }
                        break;
                    }
                }
            }

            if (isInCall == null)
            {
                foreach (var (pattern, patternName) in callPatterns)
                {
                    var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var stateStr = match.Groups["state"].Value.ToLower();
                        isInCall = stateStr is "started" or "true";
                        if (_configService.TeamsDebugMode)
                        {
                            _logger.LogDebug("[DEBUG] Found call state using pattern '{PatternName}': {IsInCall}", patternName, isInCall);
                            _logger.LogDebug("[DEBUG] Matched line: {LogLine}", line);
                        }
                        else
                        {
                            _logger.LogDebug("Found call state using pattern '{PatternName}': {IsInCall}", patternName, isInCall);
                        }
                        break;
                    }
                }
            }

            // If we found both status and call state, we can stop searching
            if (status != null && isInCall != null)
            {
                break;
            }
        }

        // Normalize status values using configuration mappings
        if (!string.IsNullOrEmpty(status))
        {
            // Apply custom mappings from configuration
            if (_configService.TeamsStatusMappings.TryGetValue(status, out var mappedStatus))
            {
                if (_configService.TeamsDebugMode)
                {
                    _logger.LogDebug("[DEBUG] Mapped status '{RawStatus}' -> '{MappedStatus}'", status, mappedStatus);
                }
                status = mappedStatus;
            }
            else
            {
                // Fallback to hardcoded defaults if not in mappings
                status = status switch
                {
                    "Away" => "Away",
                    "Available" => "Available",
                    "Busy" => "Busy",
                    "DoNotDisturb" or "DND" => "Do Not Disturb",
                    "Offline" => "Offline",
                    _ => status
                };
            }

            _logger.LogInformation("Parsed Teams status: Status={Status}, IsInCall={IsInCall}", status, isInCall ?? false);

            return new TeamsStatusUpdate
            {
                Status = status,
                IsInCall = isInCall ?? false,
                UpdatedAt = DateTime.UtcNow
            };
        }

        if (_configService.TeamsDebugMode)
        {
            _logger.LogDebug("[DEBUG] No Teams status found in {LineCount} log lines", lines.Count);
        }
        return null;
    }

    protected virtual void OnStatusChanged(string oldStatus, string newStatus)
    {
        StatusChanged?.Invoke(this, new StatusChangedEventArgs { OldStatus = oldStatus, NewStatus = newStatus });
    }

    protected virtual void OnCallActivityChanged(bool isNowInCall)
    {
        CallActivityChanged?.Invoke(this, new CallActivityChangedEventArgs { IsNowInCall = isNowInCall });
    }

    private class TeamsStatusUpdate
    {
        public required string Status { get; init; }
        public required bool IsInCall { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
