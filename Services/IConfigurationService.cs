namespace OmniPresence.Services;

public interface IConfigurationService
{
    // Application settings
    bool StartOnWindowsStartup { get; }
    
    // Home Assistant settings
    bool HomeAssistantEnabled { get; }
    string HAToken { get; }
    string HAUrl { get; }
    string StatusSensorName { get; }
    string ActivitySensorName { get; }
    
    // Teams settings
    int TeamsPollingIntervalSeconds { get; }
    bool TeamsAutoDetectLogPath { get; }
    string TeamsLogPath { get; }
    Dictionary<string, string> TeamsStatusMappings { get; }
    bool TeamsDebugMode { get; }
    
    bool IsValid();
    void SaveApplicationSettings(bool startOnWindowsStartup);
    void SaveConfiguration(string haUrl, string haToken, string statusSensorName = "sensor.teams_status", string activitySensorName = "sensor.teams_activity", bool enabled = true);
    void SaveTeamsConfiguration(int pollingIntervalSeconds, bool autoDetectLogPath, string logPath, Dictionary<string, string> statusMappings, bool debugMode);
}
