using Microsoft.Extensions.Options;

namespace SkladApi.Services;

/// <summary>
/// Глобальный ограничитель одновременных задач на локальном GPU.
/// Singleton — семафор общий для всех HTTP-запросов.
/// </summary>
public sealed class GpuSlotLimiter : IDisposable
{
    private readonly SemaphoreSlim _slots;

    public GpuSlotLimiter(IOptions<AiRouterSettings> settings)
    {
        var count = Math.Max(1, settings.Value.LocalGpuSlots);
        _slots = new SemaphoreSlim(count, count);
    }

    /// <summary>Пытается занять слот без ожидания. false — все слоты заняты.</summary>
    public Task<bool> TryAcquireAsync(CancellationToken cancellationToken) =>
        _slots.WaitAsync(TimeSpan.Zero, cancellationToken);

    /// <summary>Ждёт свободный слот.</summary>
    public Task AcquireAsync(CancellationToken cancellationToken) =>
        _slots.WaitAsync(cancellationToken);

    public void Release() => _slots.Release();

    public void Dispose() => _slots.Dispose();
}
