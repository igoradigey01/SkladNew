using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>
/// Фото накладной + текстовая подсказка приёмщика → шапка документа и позиции.
/// Текст приёмщика используется напрямую; устаревший аудиоканал расшифровывает
/// локальный Whisper. Сверку «документ ↔ факт» делает мультимодальная модель
/// в Ollama по правилам из <c>_hubx01/prompts/парсер.md</c>.
/// </summary>
public sealed class MultimodalRecognitionService : IMultimodalRecognitionService
{
    private static readonly JsonSerializerOptions DocumentJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly OllamaSettings _ollamaSettings;
    private readonly IWhisperTranscriber _transcriber;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MultimodalRecognitionService> _logger;

    public MultimodalRecognitionService(
        IOptions<OllamaSettings> ollamaOptions,
        IWhisperTranscriber transcriber,
        HttpClient httpClient,
        ILogger<MultimodalRecognitionService> logger)
    {
        _ollamaSettings = ollamaOptions.Value;
        _transcriber = transcriber;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ParsedDocumentDto> RecognizeInvoiceAsync(
        Stream? imageStream,
        Stream? audioStream,
        string? text,
        CancellationToken cancellationToken)
    {
        var transcription = await ResolveTranscriptionAsync(audioStream, text, cancellationToken);
        var imageBytes = imageStream is not null
            ? await ReadImageAsync(imageStream, cancellationToken)
            : null;

        return await AnalyzeWithOllamaAsync(imageBytes, transcription, cancellationToken);
    }

    /// <summary>
    /// Текст приёмщика из UI имеет приоритет; Whisper вызывается только для старого
    /// аудиоканала. Если нет ни текста, ни аудио — разбор идёт только по фото.
    /// </summary>
    private async Task<string> ResolveTranscriptionAsync(
        Stream? audioStream,
        string? text,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            var trimmed = text.Trim();
            _logger.LogInformation("Текст приёмщика: {Text}", trimmed);

            return trimmed;
        }

        if (audioStream is null)
        {
            _logger.LogInformation("Текст приёмщика не указан — разбор только по фото");

            return string.Empty;
        }

        return await TranscribeAsync(audioStream, cancellationToken);
    }

    private async Task<string> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await audioStream.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            return string.Empty;
        }

        if (!WavFormat.TryValidate(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), out var error))
        {
            throw new UnsupportedAudioFormatException(error!);
        }

        buffer.Position = 0;

        var transcription = await _transcriber.TranscribeAsync(buffer, cancellationToken);

        if (string.IsNullOrWhiteSpace(transcription))
        {
            _logger.LogWarning("Whisper вернул пустую расшифровку — разбор идёт только по фото");
        }
        else
        {
            _logger.LogInformation("Расшифровка: {Transcription}", transcription);
        }

        return transcription;
    }

    private static async Task<byte[]> ReadImageAsync(
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            throw new UnsupportedImageFormatException("Файл изображения пуст.");
        }

        var bytes = buffer.ToArray();

        if (!IsSupportedImage(bytes, out var error))
        {
            throw new UnsupportedImageFormatException(error!);
        }

        return bytes;
    }

    /// <summary>Хватает первых байт: модель декодирует файл сама.</summary>
    private static bool IsSupportedImage(byte[] bytes, out string? error)
    {
        var header = bytes.AsSpan(0, Math.Min(bytes.Length, 32));

        return ImageSignature.TryIdentify(header, out _, out error);
    }

    private async Task<ParsedDocumentDto> AnalyzeWithOllamaAsync(
        byte[]? imageBytes,
        string transcription,
        CancellationToken cancellationToken)
    {
        var userMessage = new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = RecognitionPrompts.BuildUserPrompt(transcription, imageBytes is not null),
        };

        if (imageBytes is { Length: > 0 })
        {
            userMessage["images"] = new[] { Convert.ToBase64String(imageBytes) };
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _ollamaSettings.Model,
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
        if (_ollamaSettings.Model.StartsWith("gemma4", StringComparison.OrdinalIgnoreCase))
        {
            payload["think"] = false;
        }

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
                ? contentProperty.GetString() ?? string.Empty
                : string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new JsonException("Модель вернула пустой ответ.");
        }

        var document = JsonSerializer.Deserialize<ParsedDocumentDto>(content, DocumentJsonOptions)
            ?? throw new JsonException("Модель вернула пустой документ.");

        document.Items ??= [];

        return document;
    }
}
