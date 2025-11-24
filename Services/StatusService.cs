using Microsoft.Extensions.Logging;

namespace OmniPresence.Services;

public class StatusService : IStatusService
{
    private readonly ILogger<StatusService> _logger;
    private readonly IStatusMonitor _monitor;
    private readonly IHomeAssistantClient _haClient;
    private readonly IConfigurationService _configService;

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;
    public event EventHandler<CallActivityChangedEventArgs>? CallActivityChanged;

    public string CurrentStatus => _monitor.CurrentStatus;
    public bool IsInCall => _monitor.IsInCall;

    public StatusService(ILogger<StatusService> logger, IStatusMonitor monitor, IHomeAssistantClient haClient, IConfigurationService configService)
    {
        _logger = logger;
        _monitor = monitor;
        _haClient = haClient;
        _configService = configService;

        _monitor.StatusChanged += OnMonitorStatusChanged;
        _monitor.CallActivityChanged += OnMonitorCallActivityChanged;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing status service");

        var haConnected = await _haClient.TestConnectionAsync(cancellationToken);
        if (!haConnected)
        {
            _logger.LogWarning("Could not connect to Home Assistant, but continuing");
        }

        await _monitor.InitializeAsync(cancellationToken);

        _logger.LogInformation("Status service initialized successfully");
    }

    private void OnMonitorStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        _logger.LogInformation("Status changed event received: {OldStatus} -> {NewStatus}", e.OldStatus, e.NewStatus);

        StatusChanged?.Invoke(this, e);

        // Fire and forget, but log any errors
        _ = SendStatusUpdateSafeAsync(e.NewStatus);
    }

    private void OnMonitorCallActivityChanged(object? sender, CallActivityChangedEventArgs e)
    {
        _logger.LogInformation("Call activity changed event received: IsInCall={IsInCall}", e.IsNowInCall);

        CallActivityChanged?.Invoke(this, e);

        // Fire and forget, but log any errors
        _ = SendCallActivityUpdateSafeAsync(e.IsNowInCall);
    }

    private async Task SendStatusUpdateSafeAsync(string status)
    {
        // Only log and send if Home Assistant is enabled
        if (!_configService.HomeAssistantEnabled)
        {
            _logger.LogDebug("Home Assistant integration is disabled, skipping status update for {Status}", status);
            return;
        }

        try
        {
            _logger.LogInformation("Sending status update to Home Assistant: {Status}", status);
            await _haClient.SendStatusUpdateAsync(status, CancellationToken.None);
            _logger.LogInformation("Status update sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send status update to Home Assistant");
        }
    }

    private async Task SendCallActivityUpdateSafeAsync(bool isInCall)
    {
        // Only log and send if Home Assistant is enabled
        if (!_configService.HomeAssistantEnabled)
        {
            _logger.LogDebug("Home Assistant integration is disabled, skipping call activity update: {Activity}", isInCall ? "In call" : "Not in call");
            return;
        }

        try
        {
            _logger.LogInformation("Sending call activity update to Home Assistant: {Activity}", isInCall ? "In call" : "Not in call");
            await _haClient.SendCallActivityUpdateAsync(isInCall, CancellationToken.None);
            _logger.LogInformation("Call activity update sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send call activity update to Home Assistant");
        }
    }
}
