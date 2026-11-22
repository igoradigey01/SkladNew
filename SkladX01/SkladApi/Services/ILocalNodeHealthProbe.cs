namespace SkladApi.Services;

/// <summary>Быстрая проверка живости локального узла (Lazy Probe).</summary>
public interface ILocalNodeHealthProbe
{
    Task<bool> IsAliveAsync(CancellationToken cancellationToken);
}
