using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>
/// Локальный провайдер разбора накладной через Ollama <c>/api/chat</c>
/// (модель gemma4:e4b и другие мультимодальные).
/// </summary>
public sealed class OllamaMultimodalProvider : ILocalAiProvider
{
    private readonly OllamaSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaMultimodalProvider> _logger;

    public OllamaMultimodalProvider(
        IOptions<OllamaSettings> settings,
        HttpClient httpClient,
        ILogger<OllamaMultimodalProvider> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsConfigured => true;

    public async Task<ParsedDocumentDto> ParseAsync(
        byte[]? imageBytes,
        string text,
        CancellationToken cancellationToken)
    {
        var hasImage = imageBytes is { Length: > 0 };

        var userMessage = new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = RecognitionPrompts.BuildUserPrompt(text, hasImage),
        };

        if (hasImage)
        {
            userMessage["images"] = new[] { Convert.ToBase64String(imageBytes!) };
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _settings.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = RecognitionPrompts.System },
                userMessage,
            },
            ["stream"] = false,
            ["format"] = RecognitionPrompts.Schema,
            ["options"] = new { temperature = 0 },
        };

        // gemma4 по умолчанию тратит время на блок рассуждений — для разбора он не нужен.
        if (_settings.Model.StartsWith("gemma4", StringComparison.OrdinalIgnoreCase))
        {
            payload["think"] = false;
        }

        _logger.LogDebug(
            "Ollama: модель {Model}, фото {HasImage}", _settings.Model, hasImage);

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/chat", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Ollama вернула {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);

        var content = result.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var contentProperty)
                ? contentProperty.GetString()
                : string.Empty;

        return RecognitionDocumentParser.Parse(content);
    }
}
