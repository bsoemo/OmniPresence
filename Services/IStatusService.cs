namespace OmniPresence.Services;

public interface IStatusService
{
    Task InitializeAsync(CancellationToken cancellationToken);
    string CurrentStatus { get; }
    bool IsInCall { get; }
    event EventHandler<StatusChangedEventArgs>? StatusChanged;
    event EventHandler<CallActivityChangedEventArgs>? CallActivityChanged;
}
