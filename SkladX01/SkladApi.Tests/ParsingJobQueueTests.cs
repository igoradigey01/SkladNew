using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class ParsingJobQueueTests
{
    [Fact]
    public async Task Enqueue_and_read_roundtrip()
    {
        var queue = new ParsingJobQueue();
        var job = new ParsingJob(Guid.NewGuid(), [1, 2, 3], "шайба сто штук", "conn-1");

        await queue.EnqueueAsync(job, default);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await foreach (var read in queue.ReadAllAsync(cts.Token))
        {
            Assert.Equal(job.Id, read.Id);
            Assert.Equal("шайба сто штук", read.Text);
            Assert.Equal("conn-1", read.ConnectionId);
            Assert.Equal([1, 2, 3], read.Image);

            break;
        }
    }
}
