namespace OmniPresence.Services;

public interface IStatusMonitor
{
    string CurrentStatus { get; }
    bool IsInCall { get; }
    event EventHandler<StatusChangedEventArgs>? StatusChanged;
    event EventHandler<CallActivityChangedEventArgs>? CallActivityChanged;
    Task InitializeAsync(CancellationToken cancellationToken);
    Task StopAsync();
}

public class StatusChangedEventArgs : EventArgs
{
    public required string OldStatus { get; init; }
    public required string NewStatus { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
}

public class CallActivityChangedEventArgs : EventArgs
{
    public required bool IsNowInCall { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
}
