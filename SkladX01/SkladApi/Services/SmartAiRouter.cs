using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>
/// Умный роутер «локальный GPU → облако». Интерактивный режим:
/// 1) занимаем слот GPU без ожидания (занят и есть облако — сразу в облако);
/// 2) если статус локального узла несвежий — быстрый зонд;
/// 3) локальный запрос, при транспортной ошибке — прозрачный fallback в DeepSeek.
/// </summary>
public sealed class SmartAiRouter : IMultimodalAiProvider
{
    private readonly ILocalAiProvider _local;
    private readonly ICloudAiProvider _cloud;
    private readonly LocalNodeStateTracker _tracker;
    private readonly ILocalNodeHealthProbe _probe;
    private readonly GpuSlotLimiter _slots;
    private readonly AiRouterSettings _settings;
    private readonly ILogger<SmartAiRouter> _logger;

    public SmartAiRouter(
        ILocalAiProvider local,
        ICloudAiProvider cloud,
        LocalNodeStateTracker tracker,
        ILocalNodeHealthProbe probe,
        GpuSlotLimiter slots,
        IOptions<AiRouterSettings> settings,
        ILogger<SmartAiRouter> logger)
    {
        _local = local;
        _cloud = cloud;
        _tracker = tracker;
        _probe = probe;
        _slots = slots;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => true;

    public async Task<ParsedDocumentDto> ParseAsync(
        byte[]? imageBytes,
        string text,
        CancellationToken cancellationToken)
    {
        _tracker.RecordUserActivity();

        var cloudReady = _cloud.IsConfigured;

        if (!await _slots.TryAcquireAsync(cancellationToken))
        {
            if (cloudReady)
            {
                _logger.LogInformation("Локальный GPU занят — запрос уходит в DeepSeek");

                return await _cloud.ParseAsync(imageBytes, text, cancellationToken);
            }

            _logger.LogInformation("Локальный GPU занят, облако не настроено — ждём слот");
            await _slots.AcquireAsync(cancellationToken);
        }

        try
        {
            if (_tracker.NeedsFreshProbe(TimeSpan.FromSeconds(_settings.ProbeFreshnessSeconds)))
            {
                var alive = await _probe.IsAliveAsync(cancellationToken);

                if (!alive)
                {
                    _tracker.MarkUnhealthy();

                    if (cloudReady)
                    {
                        _logger.LogInformation("Локальный узел не отвечает — запрос уходит в DeepSeek");

                        return await _cloud.ParseAsync(imageBytes, text, cancellationToken);
                    }

                    throw new RecognitionUnavailableException(
                        "Локальная модель недоступна, облачный провайдер не настроен.");
                }

                _tracker.MarkHealthy();
            }

            try
            {
                var document = await _local.ParseAsync(imageBytes, text, cancellationToken);
                _tracker.MarkHealthy();

                return document;
            }
            catch (Exception exception)
                when (!cancellationToken.IsCancellationRequested
                    && exception is HttpRequestException
                        or TaskCanceledException
                        or TimeoutRejectedException
                        or BrokenCircuitException)
            {
                _tracker.MarkUnhealthy();

                if (cloudReady)
                {
                    _logger.LogWarning(
                        exception, "Локальная модель упала — прозрачный fallback в DeepSeek");

                    return await _cloud.ParseAsync(imageBytes, text, cancellationToken);
                }

                throw new RecognitionUnavailableException(
                    "Локальная модель недоступна, облачный провайдер не настроен.", exception);
            }
        }
        finally
        {
            _slots.Release();
        }
    }
}
