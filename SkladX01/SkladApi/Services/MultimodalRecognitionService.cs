using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>
/// Оркестратор распознавания накладной: приводит вход к «фото + текст»,
/// затем делегирует разбор умному роутеру (локальная Ollama → облако DeepSeek).
/// Текст приёмщика используется напрямую; устаревший аудиоканал расшифровывает
/// локальный Whisper.
/// </summary>
public sealed class MultimodalRecognitionService : IMultimodalRecognitionService
{
    private readonly IWhisperTranscriber _transcriber;
    private readonly SmartAiRouter _router;
    private readonly ILogger<MultimodalRecognitionService> _logger;

    public MultimodalRecognitionService(
        IWhisperTranscriber transcriber,
        SmartAiRouter router,
        ILogger<MultimodalRecognitionService> logger)
    {
        _transcriber = transcriber;
        _router = router;
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

        return await _router.ParseAsync(imageBytes, transcription, cancellationToken);
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
}
