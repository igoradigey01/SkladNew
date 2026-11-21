using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SkladApi.Dtos;
using SkladApi.Services;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecognitionController : ControllerBase
{
    private const long MaxImageBytes = 15 * 1024 * 1024;
    private const long MaxAudioBytes = 10 * 1024 * 1024;
    private const int MaxTextLength = 4000;

    private readonly IMultimodalRecognitionService _recognitionService;
    private readonly ILogger<RecognitionController> _logger;

    public RecognitionController(
        IMultimodalRecognitionService recognitionService,
        ILogger<RecognitionController> logger)
    {
        _recognitionService = recognitionService;
        _logger = logger;
    }

    /// <summary>
    /// Фото накладной + текст приёмщика «что фактически приняли» →
    /// шапка документа и позиции с количеством по документу и фактически принятым.
    /// Аудио (WAV 16 кГц, моно, 16 бит) по-прежнему принимается для старых клиентов.
    /// </summary>
    [HttpPost("parse")]
    [RequestSizeLimit(MaxImageBytes + MaxAudioBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImageBytes + MaxAudioBytes + 1024 * 1024)]
    public async Task<ActionResult<ParsedDocumentDto>> ParseInvoice(
        [FromForm] IFormFile? image,
        [FromForm] IFormFile? audio,
        [FromForm] string? text,
        CancellationToken cancellationToken)
    {
        if ((image is null || image.Length == 0) && string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new { error = "Нужен файл фото или текст приёмщика." });
        }

        if (image is { Length: > 0 } && image.Length > MaxImageBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { error = $"Фото больше {MaxImageBytes / 1024 / 1024} МБ." });
        }

        if (audio is not null && audio.Length > 0 && audio.Length > MaxAudioBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { error = $"Аудио больше {MaxAudioBytes / 1024 / 1024} МБ." });
        }

        if (image is { Length: > 0 } && !IsImage(image))
        {
            return UnsupportedMediaTypeError(
                "Фото должно быть изображением (JPEG, PNG, WebP, HEIC).");
        }

        if (audio is not null && audio.Length > 0 && !IsAudio(audio))
        {
            return UnsupportedMediaTypeError(
                "Аудио должно быть WAV 16 кГц, моно, 16 бит — сконвертируйте запись на клиенте.");
        }

        var comment = text?.Trim();

        if (comment is { Length: > MaxTextLength })
        {
            return BadRequest(new { error = $"Текст длиннее {MaxTextLength} символов." });
        }

        _logger.LogInformation(
            "Распознавание: фото {ImageName} ({ImageSize} Б), аудио {AudioSize} Б, текст {TextLength} симв.",
            image?.FileName ?? "нет", image?.Length ?? 0, audio?.Length ?? 0, comment?.Length ?? 0);

        try
        {
            await using var imageStream = image is { Length: > 0 }
                ? image.OpenReadStream()
                : null;
            await using var audioStream = audio is { Length: > 0 }
                ? audio.OpenReadStream()
                : null;

            var document = await _recognitionService.RecognizeInvoiceAsync(
                imageStream, audioStream, comment, cancellationToken);

            return Ok(document);
        }
        catch (UnsupportedAudioFormatException exception)
        {
            _logger.LogWarning(exception, "Неподдерживаемый формат аудио");
            return UnsupportedMediaTypeError(exception.Message);
        }
        catch (UnsupportedImageFormatException exception)
        {
            _logger.LogWarning(exception, "Неподдерживаемый формат изображения");
            return UnsupportedMediaTypeError(exception.Message);
        }
        catch (WhisperUnavailableException exception)
        {
            _logger.LogError(exception, "Модель Whisper недоступна");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Ollama недоступна");
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "Сервис распознавания недоступен. Проверьте Ollama на ks34." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(
                StatusCodes.Status504GatewayTimeout,
                new { error = "Модель не ответила за отведённое время." });
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Модель вернула невалидный JSON");
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "ИИ вернул невалидный формат JSON. Повторите запрос." });
        }
    }

    private static ObjectResult UnsupportedMediaTypeError(string error)
    {
        return new ObjectResult(new { error })
        {
            StatusCode = StatusCodes.Status415UnsupportedMediaType,
        };
    }

    private static bool IsImage(IFormFile file)
    {
        return file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAudio(IFormFile file)
    {
        return file.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }
}
