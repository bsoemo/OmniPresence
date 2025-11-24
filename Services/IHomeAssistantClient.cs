namespace OmniPresence.Services;

public interface IHomeAssistantClient
{
    Task SendStatusUpdateAsync(string status, CancellationToken cancellationToken);
    Task SendCallActivityUpdateAsync(bool isInCall, CancellationToken cancellationToken);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken);
    Task<bool> TestConnectionAsync(string url, string token, CancellationToken cancellationToken);
}
