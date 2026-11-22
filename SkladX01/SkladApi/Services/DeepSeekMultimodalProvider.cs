using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>
/// Облачный провайдер разбора накладной через DeepSeek Flash
/// (OpenAI-совместимый <c>POST /chat/completions</c>, vision + JSON Output).
/// </summary>
public sealed class DeepSeekMultimodalProvider : ICloudAiProvider
{
    private readonly DeepSeekSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekMultimodalProvider> _logger;

    public DeepSeekMultimodalProvider(
        IOptions<DeepSeekSettings> settings,
        HttpClient httpClient,
        ILogger<DeepSeekMultimodalProvider> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<ParsedDocumentDto> ParseAsync(
        byte[]? imageBytes,
        string text,
        CancellationToken cancellationToken)
    {
        if (!_settings.IsConfigured)
        {
            throw new InvalidOperationException(
                "DeepSeek не настроен: задайте DeepSeek:ApiKey.");
        }

        var hasImage = imageBytes is { Length: > 0 };

        var contentParts = new List<object>
        {
            new
            {
                type = "text",
                text = RecognitionPrompts.BuildUserPrompt(text, hasImage),
            },
        };

        if (hasImage)
        {
            var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes!);
            contentParts.Add(new
            {
                type = "image_url",
                image_url = new { url = dataUrl, detail = "high" },
            });
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _settings.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = RecognitionPrompts.BuildCloudSystemPrompt() },
                new { role = "user", content = contentParts },
            },
            ["response_format"] = new { type = "json_object" },
            ["max_tokens"] = _settings.MaxTokens,
            ["temperature"] = 0,
            ["stream"] = false,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        _logger.LogDebug(
            "DeepSeek: модель {Model}, фото {HasImage}", _settings.Model, hasImage);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"DeepSeek вернул {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);

        var content = ExtractContent(result);

        return RecognitionDocumentParser.Parse(content);
    }

    private static string? ExtractContent(JsonElement result)
    {
        if (!result.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var choice = choices[0];

        return choice.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content)
                ? content.GetString()
                : null;
    }
}
