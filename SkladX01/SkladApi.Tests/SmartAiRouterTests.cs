using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkladApi.Dtos;
using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class SmartAiRouterTests
{
    [Fact]
    public async Task Uses_local_when_probe_is_alive()
    {
        var local = new FakeLocalProvider { Handler = () => Task.FromResult(Doc("local")) };
        var cloud = new FakeCloudProvider { Handler = () => throw new InvalidOperationException("облако не должно вызываться") };

        var router = CreateRouter(local, cloud);

        var document = await router.ParseAsync(null, "текст", default);

        Assert.Equal("local", document.DocumentNumber);
        Assert.Equal(1, local.Calls);
        Assert.Equal(0, cloud.Calls);
    }

    [Fact]
    public async Task Falls_back_to_cloud_when_probe_is_dead()
    {
        var local = new FakeLocalProvider();
        var cloud = new FakeCloudProvider { Handler = () => Task.FromResult(Doc("cloud")) };

        var router = CreateRouter(local, cloud, probe: new DeadProbe());

        var document = await router.ParseAsync(null, "текст", default);

        Assert.Equal("cloud", document.DocumentNumber);
        Assert.Equal(0, local.Calls);
        Assert.Equal(1, cloud.Calls);
    }

    [Fact]
    public async Task Falls_back_to_cloud_when_local_throws_transport_error()
    {
        var local = new FakeLocalProvider { Handler = () => throw new HttpRequestException("нет связи") };
        var cloud = new FakeCloudProvider { Handler = () => Task.FromResult(Doc("cloud")) };

        var router = CreateRouter(local, cloud);

        var document = await router.ParseAsync(null, "текст", default);

        Assert.Equal("cloud", document.DocumentNumber);
        Assert.Equal(1, cloud.Calls);
    }

    [Fact]
    public async Task Does_not_fall_back_on_validation_error()
    {
        var local = new FakeLocalProvider { Handler = () => throw new JsonException("не JSON") };
        var cloud = new FakeCloudProvider { Handler = () => Task.FromResult(Doc("cloud")) };

        var router = CreateRouter(local, cloud);

        await Assert.ThrowsAsync<JsonException>(
            () => router.ParseAsync(null, "текст", default));
        Assert.Equal(0, cloud.Calls);
    }

    [Fact]
    public async Task Throws_unavailable_when_local_fails_and_cloud_not_configured()
    {
        var local = new FakeLocalProvider { Handler = () => throw new HttpRequestException("нет связи") };
        var cloud = new FakeCloudProvider { IsConfigured = false };

        var router = CreateRouter(local, cloud);

        await Assert.ThrowsAsync<RecognitionUnavailableException>(
            () => router.ParseAsync(null, "текст", default));
    }

    [Fact]
    public async Task Skips_local_when_gpu_slot_is_busy_and_cloud_is_configured()
    {
        var local = new FakeLocalProvider();
        var cloud = new FakeCloudProvider { Handler = () => Task.FromResult(Doc("cloud")) };

        var settings = new AiRouterSettings { LocalGpuSlots = 1 };
        var options = Options.Create(settings);
        var slots = new GpuSlotLimiter(options);

        var router = new SmartAiRouter(
            local, cloud, new LocalNodeStateTracker(), new AliveProbe(), slots, options,
            NullLogger<SmartAiRouter>.Instance);

        Assert.True(await slots.TryAcquireAsync(default));

        try
        {
            var document = await router.ParseAsync(null, "текст", default);

            Assert.Equal("cloud", document.DocumentNumber);
            Assert.Equal(0, local.Calls);
            Assert.Equal(1, cloud.Calls);
        }
        finally
        {
            slots.Release();
        }
    }

    [Fact]
    public async Task Probes_only_once_within_freshness_window()
    {
        var local = new FakeLocalProvider();
        var cloud = new FakeCloudProvider();
        var probe = new CountingProbe();

        var router = CreateRouter(local, cloud, probe: probe);

        await router.ParseAsync(null, "текст", default);
        await router.ParseAsync(null, "текст", default);

        Assert.Equal(1, probe.Calls);
        Assert.Equal(2, local.Calls);
    }

    private static SmartAiRouter CreateRouter(
        FakeLocalProvider local,
        FakeCloudProvider cloud,
        LocalNodeStateTracker? tracker = null,
        ILocalNodeHealthProbe? probe = null,
        AiRouterSettings? settings = null)
    {
        settings ??= new AiRouterSettings();
        var options = Options.Create(settings);

        return new SmartAiRouter(
            local,
            cloud,
            tracker ?? new LocalNodeStateTracker(),
            probe ?? new AliveProbe(),
            new GpuSlotLimiter(options),
            options,
            NullLogger<SmartAiRouter>.Instance);
    }

    private static ParsedDocumentDto Doc(string number) =>
        new() { DocumentNumber = number };

    private abstract class FakeProviderBase : IMultimodalAiProvider
    {
        public bool IsConfigured { get; set; } = true;

        public int Calls { get; private set; }

        public Func<Task<ParsedDocumentDto>> Handler { get; set; } =
            () => Task.FromResult(new ParsedDocumentDto());

        public Task<ParsedDocumentDto> ParseAsync(
            byte[]? imageBytes, string text, CancellationToken cancellationToken)
        {
            Calls++;

            return Handler();
        }
    }

    private sealed class FakeLocalProvider : FakeProviderBase, ILocalAiProvider { }

    private sealed class FakeCloudProvider : FakeProviderBase, ICloudAiProvider { }

    private sealed class AliveProbe : ILocalNodeHealthProbe
    {
        public Task<bool> IsAliveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class DeadProbe : ILocalNodeHealthProbe
    {
        public Task<bool> IsAliveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class CountingProbe : ILocalNodeHealthProbe
    {
        public int Calls { get; private set; }

        public Task<bool> IsAliveAsync(CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(true);
        }
    }
}
