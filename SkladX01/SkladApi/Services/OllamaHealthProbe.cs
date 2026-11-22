namespace SkladApi.Services;

/// <summary>
/// Зонд Ollama: <c>GET /api/tags</c>. HttpClient настроен с жёстким таймаутом
/// (по умолчанию 600 мс), поэтому любой сбой — это «узел недоступен».
/// </summary>
public sealed class OllamaHealthProbe : ILocalNodeHealthProbe
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaHealthProbe> _logger;

    public OllamaHealthProbe(HttpClient httpClient, ILogger<OllamaHealthProbe> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IsAliveAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/tags", cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogInformation("Локальный узел не отвечает: {Message}", exception.Message);

            return false;
        }
    }
}
