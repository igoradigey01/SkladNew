using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class DeepSeekMultimodalProviderTests
{
    [Fact]
    public async Task Sends_json_object_with_image_block()
    {
        var handler = new CapturingHandler(Choices("""{"document_number":"1042","items":[]}"""));
        var provider = Create(handler, apiKey: "secret");

        var document = await provider.ParseAsync([1, 2, 3], "шайба сто штук", default);

        Assert.Equal("1042", document.DocumentNumber);
        Assert.Equal("Bearer secret", handler.Authorization);

        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;

        Assert.Equal("deepseek-flash", root.GetProperty("model").GetString());
        Assert.Equal(
            "json_object",
            root.GetProperty("response_format").GetProperty("type").GetString());

        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains(
            "json",
            messages[0].GetProperty("content").GetString()!.ToLowerInvariant());

        var content = messages[1].GetProperty("content");
        Assert.Equal(JsonValueKind.Array, content.ValueKind);

        var textPart = content[0];
        Assert.Equal("text", textPart.GetProperty("type").GetString());
        Assert.Contains("шайба сто штук", textPart.GetProperty("text").GetString());

        var imagePart = content[1];
        Assert.Equal("image_url", imagePart.GetProperty("type").GetString());
        Assert.StartsWith(
            "data:image/jpeg;base64,",
            imagePart.GetProperty("image_url").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Omits_image_block_when_image_is_absent()
    {
        var handler = new CapturingHandler(Choices("""{"items":[]}"""));
        var provider = Create(handler, apiKey: "secret");

        await provider.ParseAsync(null, "только текст", default);

        using var body = JsonDocument.Parse(handler.Body!);
        var content = body.RootElement.GetProperty("messages")[1].GetProperty("content");

        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal("text", content[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task Throws_when_not_configured()
    {
        var provider = Create(new CapturingHandler(Choices("{}")), apiKey: string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.ParseAsync(null, "текст", default));
    }

    [Fact]
    public async Task Throws_on_empty_content()
    {
        var provider = Create(new CapturingHandler(Choices(string.Empty)), apiKey: "secret");

        await Assert.ThrowsAsync<JsonException>(
            () => provider.ParseAsync(null, "текст", default));
    }

    private static DeepSeekMultimodalProvider Create(CapturingHandler handler, string apiKey)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com"),
        };

        return new DeepSeekMultimodalProvider(
            Options.Create(new DeepSeekSettings { ApiKey = apiKey, Model = "deepseek-flash" }),
            client,
            NullLogger<DeepSeekMultimodalProvider>.Instance);
    }

    private static string Choices(string? content) =>
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } },
        });

    private sealed class CapturingHandler(string responseBody) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
