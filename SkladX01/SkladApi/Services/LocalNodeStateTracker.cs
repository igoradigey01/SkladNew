namespace SkladApi.Services;

/// <summary>
/// Трекер состояния локального узла (Ollama/Whisper). Хранит время последней
/// активности и последнего успеха, чтобы зонд перед запросом был «ленивым».
/// </summary>
public sealed class LocalNodeStateTracker
{
    private long _lastActivityTicks = DateTime.UtcNow.Ticks;
    private long _lastSuccessTicks = DateTime.MinValue.Ticks;
    private volatile bool _isHealthy;

    public void RecordUserActivity() =>
        Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

    public bool IsIdle(TimeSpan idleThreshold) =>
        DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastActivityTicks)) > idleThreshold;

    public bool NeedsFreshProbe(TimeSpan cacheTtl) =>
        !_isHealthy
        || (DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastSuccessTicks)) > cacheTtl);

    public void MarkHealthy()
    {
        _isHealthy = true;
        Interlocked.Exchange(ref _lastSuccessTicks, DateTime.UtcNow.Ticks);
    }

    public void MarkUnhealthy() => _isHealthy = false;
}
