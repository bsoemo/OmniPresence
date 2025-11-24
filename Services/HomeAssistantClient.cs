using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace OmniPresence.Services;

public class HomeAssistantClient : IHomeAssistantClient
{
    private readonly ILogger<HomeAssistantClient> _logger;
    private readonly IConfigurationService _configService;
    private readonly HttpClient _httpClient;

    public HomeAssistantClient(ILogger<HomeAssistantClient> logger, IConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
        _httpClient = new HttpClient();
    }

    public async Task SendStatusUpdateAsync(string status, CancellationToken cancellationToken)
    {
        try
        {
            if (!_configService.HomeAssistantEnabled)
            {
                _logger.LogDebug("Home Assistant integration is disabled, skipping status update");
                return;
            }

            if (!_configService.IsValid())
            {
                _logger.LogWarning("Configuration is invalid, cannot send status update. URL: {URL}, Token present: {HasToken}", 
                    _configService.HAUrl, !string.IsNullOrEmpty(_configService.HAToken));
                return;
            }

            var url = $"{_configService.HAUrl}/api/states/{_configService.StatusSensorName}";
            _logger.LogDebug("Sending status update to: {URL}", url);
            
            var payload = new
            {
                state = status,
                attributes = new
                {
                    friendly_name = "Teams Status",
                    icon = "mdi:microsoft-teams",
                    unit_of_measurement = ""
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("Authorization", $"Bearer {_configService.HAToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Status update sent successfully to Home Assistant: {Status}", status);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send status update to Home Assistant: {StatusCode} - {Response}", 
                    response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending status update to Home Assistant");
        }
    }

    public async Task SendCallActivityUpdateAsync(bool isInCall, CancellationToken cancellationToken)
    {
        try
        {
            if (!_configService.HomeAssistantEnabled)
            {
                _logger.LogDebug("Home Assistant integration is disabled, skipping call activity update");
                return;
            }

            if (!_configService.IsValid())
            {
                _logger.LogWarning("Configuration is invalid, cannot send call activity update. URL: {URL}, Token present: {HasToken}", 
                    _configService.HAUrl, !string.IsNullOrEmpty(_configService.HAToken));
                return;
            }

            var activity = isInCall ? "In a call" : "Not in a call";
            var url = $"{_configService.HAUrl}/api/states/{_configService.ActivitySensorName}";
            _logger.LogDebug("Sending call activity update to: {URL}", url);
            
            var payload = new
            {
                state = activity,
                attributes = new
                {
                    friendly_name = "Teams Activity",
                    icon = isInCall ? "mdi:phone-in-talk" : "mdi:phone-off",
                    unit_of_measurement = ""
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("Authorization", $"Bearer {_configService.HAToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Call activity update sent successfully to Home Assistant: {Activity}", activity);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send call activity update to Home Assistant: {StatusCode} - {Response}", 
                    response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending call activity update to Home Assistant");
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_configService.HomeAssistantEnabled)
            {
                _logger.LogWarning("Home Assistant integration is disabled, cannot test connection");
                return false;
            }

            if (!_configService.IsValid())
            {
                _logger.LogWarning("Configuration is invalid, cannot test connection");
                return false;
            }

            return await TestConnectionAsync(_configService.HAUrl, _configService.HAToken, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Home Assistant connection");
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(string url, string token, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("URL or token is empty, cannot test connection");
                return false;
            }

            var testUrl = $"{url}/api/";
            var message = new HttpRequestMessage(HttpMethod.Get, testUrl);
            message.Headers.Add("Authorization", $"Bearer {token}");

            var response = await _httpClient.SendAsync(message, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Home Assistant connection test successful");
                return true;
            }
            else
            {
                _logger.LogError("Home Assistant connection test failed: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Home Assistant connection with provided credentials");
            return false;
        }
    }
}
