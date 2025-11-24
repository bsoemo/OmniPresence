using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.IO;
using System.Diagnostics;

namespace OmniPresence.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly WindowsStartupService _startupService;
    private IConfiguration _configuration;
    private readonly string _userSecretsId = "OmniPresence-3c7f5e8d-4b2a-9c1f-6e3a-7d9f2c5b1a8e";

    // Application settings (with sensible defaults)
    public bool StartOnWindowsStartup { get; private set; } = false;
    
    // Home Assistant settings (with sensible defaults)
    public bool HomeAssistantEnabled { get; private set; } = true;  // Enabled by default, but non-functional until configured
    public string HAToken { get; private set; } = string.Empty;
    public string HAUrl { get; private set; } = "http://localhost:8123";  // Common default
    public string StatusSensorName { get; private set; } = "sensor.teams_status";
    public string ActivitySensorName { get; private set; } = "sensor.teams_activity";
    
    // Teams settings (with sensible defaults)
    public int TeamsPollingIntervalSeconds { get; private set; } = 2;
    public bool TeamsAutoDetectLogPath { get; private set; } = true;  // Auto-detect by default
    public string TeamsLogPath { get; private set; } = string.Empty;
    public Dictionary<string, string> TeamsStatusMappings { get; private set; } = new();
    public bool TeamsDebugMode { get; private set; } = false;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _startupService = new WindowsStartupService(logger);
        
        // Build initial configuration (only from User Secrets)
        _configuration = BuildConfiguration();
        LoadConfiguration();
    }

    private IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<ConfigurationService>(optional: true);
        
        return builder.Build();
    }

    private void LoadConfiguration()
    {
        try
        {
            _logger.LogInformation("Loading configuration from User Secrets with sensible defaults...");

            // Load Application settings
            if (bool.TryParse(_configuration["Application:StartOnWindowsStartup"], out var startOnStartup))
            {
                StartOnWindowsStartup = startOnStartup;
            }
            
            // Load Home Assistant settings
            if (bool.TryParse(_configuration["Services:HomeAssistant:Enabled"], out var haEnabled))
            {
                HomeAssistantEnabled = haEnabled;
            }
            
            var loadedUrl = _configuration["Services:HomeAssistant:Url"];
            if (!string.IsNullOrEmpty(loadedUrl))
            {
                HAUrl = loadedUrl;
            }
            
            var loadedToken = _configuration["Services:HomeAssistant:Token"];
            if (!string.IsNullOrEmpty(loadedToken))
            {
                HAToken = loadedToken;
            }
            
            var loadedStatusSensor = _configuration["Services:HomeAssistant:StatusSensorName"];
            if (!string.IsNullOrEmpty(loadedStatusSensor))
            {
                StatusSensorName = loadedStatusSensor;
            }
            
            var loadedActivitySensor = _configuration["Services:HomeAssistant:ActivitySensorName"];
            if (!string.IsNullOrEmpty(loadedActivitySensor))
            {
                ActivitySensorName = loadedActivitySensor;
            }

            // Load Teams settings
            if (int.TryParse(_configuration["Services:Teams:PollingIntervalSeconds"], out var pollingInterval))
            {
                TeamsPollingIntervalSeconds = pollingInterval;
            }
            
            if (bool.TryParse(_configuration["Services:Teams:AutoDetectLogPath"], out var autoDetect))
            {
                TeamsAutoDetectLogPath = autoDetect;
            }
            
            var loadedLogPath = _configuration["Services:Teams:LogPath"];
            TeamsLogPath = !string.IsNullOrEmpty(loadedLogPath) ? loadedLogPath : GetDefaultTeamsLogPath();
            
            // Load status mappings
            var mappingsJson = _configuration["Services:Teams:StatusMappings"];
            if (!string.IsNullOrEmpty(mappingsJson))
            {
                try
                {
                    TeamsStatusMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingsJson) ?? GetDefaultStatusMappings();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize status mappings, using defaults");
                    TeamsStatusMappings = GetDefaultStatusMappings();
                }
            }
            else
            {
                TeamsStatusMappings = GetDefaultStatusMappings();
            }
            
            if (bool.TryParse(_configuration["Services:Teams:DebugMode"], out var debugMode))
            {
                TeamsDebugMode = debugMode;
            }

            _logger.LogInformation("Configuration loaded successfully with sensible defaults");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration - using all defaults");
        }
    }

    public bool IsValid()
    {
        // Valid only if user has configured a token and a custom URL (not the default)
        return !string.IsNullOrWhiteSpace(HAToken) && !string.IsNullOrWhiteSpace(HAUrl) && HAUrl != "http://localhost:8123";
    }

    public void SaveApplicationSettings(bool startOnWindowsStartup)
    {
        try
        {
            _logger.LogInformation("Saving application settings to User Secrets");
            
            try
            {
                SaveApplicationSettingsToUserSecretsFile(startOnWindowsStartup);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save application settings directly to User Secrets file");
                SaveApplicationSettingsViaUserSecretsCommand(startOnWindowsStartup);
            }
            
            // Update in-memory value
            StartOnWindowsStartup = startOnWindowsStartup;
            
            // Set Windows startup registry entry
            _logger.LogInformation("Setting Windows startup registry entry: {StartOnWindowsStartup}", startOnWindowsStartup);
            var startupSuccess = _startupService.SetStartupEnabled(startOnWindowsStartup);
            if (startupSuccess)
            {
                _logger.LogInformation("Windows startup registry entry set successfully");
            }
            else
            {
                _logger.LogWarning("Failed to set Windows startup registry entry. Admin rights may be required.");
            }
            
            // Reload configuration
            ReloadApplicationSettings();
            
            _logger.LogInformation("Application settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving application settings");
            throw;
        }
    }

    private void ReloadApplicationSettings()
    {
        try
        {
            _configuration = BuildConfiguration();
            
            if (bool.TryParse(_configuration["Application:StartOnWindowsStartup"], out var startOnStartup))
            {
                StartOnWindowsStartup = startOnStartup;
                _logger.LogInformation("Reloaded start on Windows startup: {StartOnStartup}", startOnStartup);
            }
            
            _logger.LogInformation("Application settings reloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading application settings");
        }
    }

    private void SaveApplicationSettingsToUserSecretsFile(bool startOnWindowsStartup)
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userSecretsPath = Path.Combine(appDataPath, "Microsoft", "UserSecrets", _userSecretsId, "secrets.json");
            
            _logger.LogDebug("Saving application settings to User Secrets file: {Path}", userSecretsPath);
            
            var secrets = new Dictionary<string, string>();
            
            if (File.Exists(userSecretsPath))
            {
                try
                {
                    var json = File.ReadAllText(userSecretsPath);
                    secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read existing secrets file, will create new");
                    secrets = new Dictionary<string, string>();
                }
            }
            else
            {
                var directory = Path.GetDirectoryName(userSecretsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }
            }
            
            // Update application settings
            secrets["Application:StartOnWindowsStartup"] = startOnWindowsStartup.ToString();
            
            var json_output = JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(userSecretsPath, json_output);
            
            _logger.LogInformation("Application settings saved directly to User Secrets file: StartOnWindowsStartup={StartOnWindowsStartup}", 
                startOnWindowsStartup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save application settings directly to User Secrets file");
            throw;
        }
    }

    private void SaveApplicationSettingsViaUserSecretsCommand(bool startOnWindowsStartup)
    {
        try
        {
            var projectPath = AppDomain.CurrentDomain.BaseDirectory;
            var projectFile = Path.Combine(projectPath, "OmniPresence.csproj");
            
            if (!File.Exists(projectFile))
            {
                projectFile = Path.Combine(projectPath, "..", "OmniPresence.csproj");
                projectFile = Path.GetFullPath(projectFile);
            }
            
            ExecuteUserSecretsCommand($"user-secrets set \"Application:StartOnWindowsStartup\" \"{startOnWindowsStartup}\" --project \"{projectFile}\"");
            
            _logger.LogInformation("Application settings saved via dotnet user-secrets command");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save application settings via user-secrets command");
            throw;
        }
    }

    public void SaveTeamsConfiguration(int pollingIntervalSeconds, bool autoDetectLogPath, string logPath, Dictionary<string, string> statusMappings, bool debugMode)
    {
        try
        {
            if (pollingIntervalSeconds < 1 || pollingIntervalSeconds > 300)
            {
                _logger.LogError("Invalid polling interval: {Interval}", pollingIntervalSeconds);
                throw new ArgumentException("Polling interval must be between 1 and 300 seconds");
            }

            _logger.LogInformation("Saving Teams configuration to User Secrets");
            
            try
            {
                SaveTeamsToUserSecretsFile(pollingIntervalSeconds, autoDetectLogPath, logPath, statusMappings, debugMode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save Teams config directly to User Secrets file");
                SaveTeamsViaUserSecretsCommand(pollingIntervalSeconds, autoDetectLogPath, logPath, statusMappings, debugMode);
            }
            
            // Update in-memory values
            TeamsPollingIntervalSeconds = pollingIntervalSeconds;
            TeamsAutoDetectLogPath = autoDetectLogPath;
            TeamsLogPath = logPath;
            TeamsStatusMappings = statusMappings;
            TeamsDebugMode = debugMode;
            
            // Reload configuration
            ReloadTeamsConfiguration();
            
            _logger.LogInformation("Teams configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Teams configuration");
            throw;
        }
    }

    private void ReloadTeamsConfiguration()
    {
        try
        {
            // Rebuild the configuration to pick up changes from User Secrets
            _configuration = BuildConfiguration();
            
            if (int.TryParse(_configuration["Services:Teams:PollingIntervalSeconds"], out var pollingInterval))
            {
                TeamsPollingIntervalSeconds = pollingInterval;
                _logger.LogInformation("Reloaded polling interval: {Interval} seconds", pollingInterval);
            }
            
            if (bool.TryParse(_configuration["Services:Teams:AutoDetectLogPath"], out var autoDetect))
            {
                TeamsAutoDetectLogPath = autoDetect;
                _logger.LogInformation("Reloaded auto-detect log path: {AutoDetect}", autoDetect);
            }
            
            TeamsLogPath = _configuration["Services:Teams:LogPath"] ?? GetDefaultTeamsLogPath();
            _logger.LogInformation("Reloaded Teams log path: {LogPath}", TeamsLogPath);
            
            var mappingsJson = _configuration["Services:Teams:StatusMappings"];
            if (!string.IsNullOrEmpty(mappingsJson))
            {
                try
                {
                    TeamsStatusMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingsJson) ?? GetDefaultStatusMappings();
                    _logger.LogInformation("Reloaded status mappings: {MappingCount} entries", TeamsStatusMappings.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize status mappings, using defaults");
                    TeamsStatusMappings = GetDefaultStatusMappings();
                }
            }
            else
            {
                TeamsStatusMappings = GetDefaultStatusMappings();
            }
            
            if (bool.TryParse(_configuration["Services:Teams:DebugMode"], out var debugMode))
            {
                TeamsDebugMode = debugMode;
                _logger.LogInformation("Reloaded debug mode: {DebugMode}", debugMode);
            }
            
            _logger.LogInformation("Teams configuration reloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading Teams configuration");
        }
    }

    private void SaveTeamsToUserSecretsFile(int pollingIntervalSeconds, bool autoDetectLogPath, string logPath, Dictionary<string, string> statusMappings, bool debugMode)
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userSecretsPath = Path.Combine(appDataPath, "Microsoft", "UserSecrets", _userSecretsId, "secrets.json");
            
            _logger.LogDebug("Saving Teams config to User Secrets file: {Path}", userSecretsPath);
            
            var secrets = new Dictionary<string, string>();
            
            if (File.Exists(userSecretsPath))
            {
                try
                {
                    var json = File.ReadAllText(userSecretsPath);
                    secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read existing secrets file, will create new");
                    secrets = new Dictionary<string, string>();
                }
            }
            else
            {
                var directory = Path.GetDirectoryName(userSecretsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }
            }
            
            // Update Teams settings
            secrets["Services:Teams:PollingIntervalSeconds"] = pollingIntervalSeconds.ToString();
            secrets["Services:Teams:AutoDetectLogPath"] = autoDetectLogPath.ToString();
            secrets["Services:Teams:LogPath"] = logPath;
            secrets["Services:Teams:StatusMappings"] = JsonSerializer.Serialize(statusMappings);
            secrets["Services:Teams:DebugMode"] = debugMode.ToString();
            
            var json_output = JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(userSecretsPath, json_output);
            
            _logger.LogInformation("Teams configuration saved directly to User Secrets file: PollingInterval={Interval}s, AutoDetect={AutoDetect}, DebugMode={DebugMode}", 
                pollingIntervalSeconds, autoDetectLogPath, debugMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Teams configuration directly to User Secrets file");
            throw;
        }
    }

    private void SaveTeamsViaUserSecretsCommand(int pollingIntervalSeconds, bool autoDetectLogPath, string logPath, Dictionary<string, string> statusMappings, bool debugMode)
    {
        try
        {
            var projectPath = AppDomain.CurrentDomain.BaseDirectory;
            var projectFile = Path.Combine(projectPath, "OmniPresence.csproj");
            
            if (!File.Exists(projectFile))
            {
                projectFile = Path.Combine(projectPath, "..", "OmniPresence.csproj");
                projectFile = Path.GetFullPath(projectFile);
            }
            
            ExecuteUserSecretsCommand($"user-secrets set \"Services:Teams:PollingIntervalSeconds\" \"{pollingIntervalSeconds}\" --project \"{projectFile}\"");
            ExecuteUserSecretsCommand($"user-secrets set \"Services:Teams:AutoDetectLogPath\" \"{autoDetectLogPath}\" --project \"{projectFile}\"");
            ExecuteUserSecretsCommand($"user-secrets set \"Services:Teams:LogPath\" \"{EscapeForShell(logPath)}\" --project \"{projectFile}\"");
            ExecuteUserSecretsCommand($"user-secrets set \"Services:Teams:StatusMappings\" \"{EscapeForShell(JsonSerializer.Serialize(statusMappings))}\" --project \"{projectFile}\"");
            ExecuteUserSecretsCommand($"user-secrets set \"Services:Teams:DebugMode\" \"{debugMode}\" --project \"{projectFile}\"");
            
            _logger.LogInformation("Teams configuration saved via dotnet user-secrets command");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Teams configuration via user-secrets command");
            throw;
        }
    }

    private string GetDefaultTeamsLogPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Packages", "MSTeams_8wekyb3d8bbwe", "LocalCache", "Microsoft", "MSTeams", "Logs");
    }

    private Dictionary<string, string> GetDefaultStatusMappings()
    {
        return new Dictionary<string, string>
        {
            { "Available", "Available" },
            { "Away", "Away" },
            { "Busy", "Busy" },
            { "DoNotDisturb", "Do Not Disturb" },
            { "DND", "Do Not Disturb" },
            { "Offline", "Offline" }
        };
    }

    public void SaveConfiguration(string haUrl, string haToken, string statusSensorName = "sensor.teams_status", string activitySensorName = "sensor.teams_activity", bool enabled = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(haUrl) || string.IsNullOrWhiteSpace(haToken))
            {
                _logger.LogError("Cannot save configuration: URL or token is empty");
                throw new ArgumentException("URL and token cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(statusSensorName) || string.IsNullOrWhiteSpace(activitySensorName))
            {
                _logger.LogError("Cannot save configuration: sensor names cannot be empty");
                throw new ArgumentException("Sensor names cannot be empty");
            }

            _logger.LogInformation("Saving configuration to User Secrets");
            
            try
            {
                SaveToUserSecretsFile(haUrl, haToken, statusSensorName, activitySensorName, enabled);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save directly to User Secrets file, attempting via dotnet CLI");
                SaveViaUserSecretsCommand(haUrl, haToken, statusSensorName, activitySensorName, enabled);
            }
            
            HAUrl = haUrl;
            HAToken = haToken;
            StatusSensorName = statusSensorName;
            ActivitySensorName = activitySensorName;
            HomeAssistantEnabled = enabled;
            
            ReloadConfiguration();
            
            _logger.LogInformation("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            throw;
        }
    }

    private void ReloadConfiguration()
    {
        try
        {
            _configuration = BuildConfiguration();
            
            if (bool.TryParse(_configuration["Services:HomeAssistant:Enabled"], out var haEnabled))
            {
                HomeAssistantEnabled = haEnabled;
            }
            
            var loadedToken = _configuration["Services:HomeAssistant:Token"];
            HAToken = !string.IsNullOrEmpty(loadedToken) ? loadedToken : string.Empty;
            
            var loadedUrl = _configuration["Services:HomeAssistant:Url"];
            HAUrl = !string.IsNullOrEmpty(loadedUrl) ? loadedUrl : "http://localhost:8123";
            
            StatusSensorName = _configuration["Services:HomeAssistant:StatusSensorName"] ?? "sensor.teams_status";
            ActivitySensorName = _configuration["Services:HomeAssistant:ActivitySensorName"] ?? "sensor.teams_activity";
            
            _logger.LogInformation("Configuration reloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading configuration");
        }
    }

    private void SaveToUserSecretsFile(string haUrl, string haToken, string statusSensorName, string activitySensorName, bool enabled)
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userSecretsPath = Path.Combine(appDataPath, "Microsoft", "UserSecrets", _userSecretsId, "secrets.json");
            
            var secrets = ReadOrCreateSecretsFile(userSecretsPath);
            
            secrets["Services:HomeAssistant:Enabled"] = enabled.ToString();
            secrets["Services:HomeAssistant:Url"] = haUrl;
            secrets["Services:HomeAssistant:Token"] = haToken;
            secrets["Services:HomeAssistant:StatusSensorName"] = statusSensorName;
            secrets["Services:HomeAssistant:ActivitySensorName"] = activitySensorName;
            
            WriteSecretsFile(userSecretsPath, secrets);
            
            _logger.LogInformation("HA configuration saved to User Secrets file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save HA configuration to User Secrets file");
            throw;
        }
    }

    private void SaveViaUserSecretsCommand(string haUrl, string haToken, string statusSensorName, string activitySensorName, bool enabled)
    {
        try
        {
            var projectPath = AppDomain.CurrentDomain.BaseDirectory;
            var projectFile = Path.Combine(projectPath, "OmniPresence.csproj");
            
            if (!File.Exists(projectFile))
            {
                projectFile = Path.Combine(projectPath, "..", "OmniPresence.csproj");
                projectFile = Path.GetFullPath(projectFile);
            }

            ExecuteUserSecretsCommand($"user-secrets set \"Services:HomeAssistant:Enabled\" \"{enabled}\" --project \"{projectFile}\"");
            ExecuteUserSecretsCommand($"user-secrets set \"Services:HomeAssistant:Url\" \"{EscapeForShell(haUrl)}\" --project \"{projectFile}\"");
            ExecuteUserSecretsCommand($"user-secrets set \"Services:HomeAssistant:Token\" \"{EscapeForShell(haToken)}\" --project \"{projectFile}\"");
            ExecuteUserSecretsCommand($"user-secrets set \"Services:HomeAssistant:StatusSensorName\" \"{EscapeForShell(statusSensorName)}\" --project \"{projectFile}\"");
            ExecuteUserSecretsCommand($"user-secrets set \"Services:HomeAssistant:ActivitySensorName\" \"{EscapeForShell(activitySensorName)}\" --project \"{projectFile}\"");
            
            _logger.LogInformation("Configuration saved via user-secrets command");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save via user-secrets command");
            throw;
        }
    }

    private Dictionary<string, string> ReadOrCreateSecretsFile(string userSecretsPath)
    {
        var secrets = new Dictionary<string, string>();
        
        if (File.Exists(userSecretsPath))
        {
            try
            {
                var json = File.ReadAllText(userSecretsPath);
                secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read existing secrets file, will create new");
            }
        }
        else
        {
            var directory = Path.GetDirectoryName(userSecretsPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }
        }
        
        return secrets;
    }

    private void WriteSecretsFile(string userSecretsPath, Dictionary<string, string> secrets)
    {
        var json_output = JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(userSecretsPath, json_output);
    }

    private string ExecuteUserSecretsCommand(string arguments)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using (var process = Process.Start(processInfo))
            {
                if (process == null)
                {
                    _logger.LogError("Failed to start dotnet process");
                    return "Failed to start process";
                }
                
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                var exitCode = process.ExitCode;
                
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("User secrets error: {Error}", error);
                }
                
                return exitCode == 0 ? "Success" : $"Failed with code {exitCode}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing user-secrets command");
            return "Exception occurred";
        }
    }

    private static string EscapeForShell(string value)
    {
        return value.Replace("\"", "\\\"");
    }
}
