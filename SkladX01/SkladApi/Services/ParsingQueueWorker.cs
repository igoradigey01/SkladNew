using Microsoft.AspNetCore.SignalR;
using SkladApi.Dtos;
using SkladApi.Hubs;

namespace SkladApi.Services;

/// <summary>Результат асинхронного разбора, который уходит клиенту по SignalR.</summary>
public sealed record ParsingResultDto(Guid JobId, ParsedDocumentDto? Document, string? Error);

/// <summary>
/// Фоновый воркер очереди: забирает задачу, разбирает через SmartAiRouter
/// (локально → облако) и пушит результат в SignalR-соединение клиента.
/// </summary>
public sealed class ParsingQueueWorker : BackgroundService
{
    private readonly ParsingJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ParsingHub> _hub;
    private readonly ILogger<ParsingQueueWorker> _logger;

    public ParsingQueueWorker(
        ParsingJobQueue queue,
        IServiceScopeFactory scopeFactory,
        IHubContext<ParsingHub> hub,
        ILogger<ParsingQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Воркер очереди разбора запущен");

        try
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатное завершение при остановке приложения.
        }
    }

    private async Task ProcessAsync(ParsingJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Очередь: разбор {JobId}, фото {HasImage}", job.Id, job.Image is not null);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<SmartAiRouter>();

            var document = await router.ParseAsync(job.Image, job.Text, cancellationToken);

            _logger.LogInformation("Очередь: {JobId} готов, позиций {Count}", job.Id, document.Items.Count);

            await NotifyAsync(job, document, null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Остановка приложения — результат не нужен.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Очередь: разбор {JobId} завершился ошибкой", job.Id);

            await NotifyAsync(
                job, null, "Не удалось разобрать накладную. Повторите позже.", cancellationToken);
        }
    }

    private Task NotifyAsync(
        ParsingJob job,
        ParsedDocumentDto? document,
        string? error,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.ConnectionId))
        {
            return Task.CompletedTask;
        }

        var payload = new ParsingResultDto(job.Id, document, error);

        return _hub.Clients
            .Client(job.ConnectionId)
            .SendAsync("parsingResult", payload, cancellationToken);
    }
}
