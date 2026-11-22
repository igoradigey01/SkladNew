using System.Threading.Channels;

namespace SkladApi.Services;

/// <summary>
/// Очередь тяжёлых задач разбора накладных (Channel&lt;T&gt;).
/// Singleton: HTTP-запрос кладёт задачу, фоновый воркер забирает.
/// </summary>
public sealed class ParsingJobQueue
{
    public const int Capacity = 64;

    private readonly Channel<ParsingJob> _channel =
        Channel.CreateBounded<ParsingJob>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ValueTask EnqueueAsync(ParsingJob job, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    public IAsyncEnumerable<ParsingJob> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
