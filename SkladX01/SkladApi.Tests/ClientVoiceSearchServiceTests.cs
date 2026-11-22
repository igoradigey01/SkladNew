using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class ClientVoiceSearchServiceTests
{
    private static readonly КлиентDto[] Clients =
    [
        new() { Id = 33, Псевдоним = "Алишер  Белоречка", Фио = "none", Телефон = "89182507671", Город = "Родники", Активен = true },
        new() { Id = 81, Псевдоним = "Андрей  БЕЛОР", Фио = "none", Телефон = "89884617004", Город = "Белореченск", Активен = true },
        new() { Id = 5, Псевдоним = "Саша Родники", Фио = "none", Телефон = "89184304094", Город = "Белореченск", Активен = true },
    ];

    [Fact]
    public async Task Finds_client_by_extracted_name()
    {
        var service = Create("""{"name":"алишер белоречка","city":"","phone":""}""");

        var candidates = await service.SearchAsync("найди клиента алишер белоречка", default);

        Assert.Equal(33, candidates[0].Id);
        Assert.Equal(100, candidates[0].Score);
        Assert.Equal("alias", candidates[0].MatchedField);
    }

    [Fact]
    public async Task Finds_client_by_city_in_case()
    {
        var service = Create("""{"name":"","city":"Родники","phone":""}""");

        var candidates = await service.SearchAsync("клиент из родников", default);

        Assert.Equal(33, candidates[0].Id);
        Assert.Equal("city", candidates[0].MatchedField);
    }

    [Fact]
    public async Task Parses_phone_spoken_as_words_when_model_returns_nothing()
    {
        var service = Create("""{"name":"","city":"","phone":""}""");

        var candidates = await service.SearchAsync(
            "восемь девять один восемь два пять ноль семь шесть семь один",
            default);

        Assert.Equal(33, candidates[0].Id);
        Assert.Equal("phone", candidates[0].MatchedField);
        Assert.Equal(100, candidates[0].Score);
    }

    [Fact]
    public async Task Falls_back_to_text_search_when_model_is_down()
    {
        var service = Create(body: null);

        var candidates = await service.SearchAsync("алишер белоречка", default);

        Assert.Equal(33, candidates[0].Id);
    }

    [Fact]
    public async Task Uses_cloud_when_local_model_is_down()
    {
        var cloudBody = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = """{"name":"алишер белоречка","city":"","phone":""}""",
                    },
                },
            },
        });

        var factory = new NamedFactory(new Dictionary<string, HttpClient>
        {
            ["ollama"] = new HttpClient(new FailingHandler()),
            ["deepseek"] = new HttpClient(new StubHandler(cloudBody))
            {
                BaseAddress = new Uri("https://api.deepseek.com"),
            },
        });

        var service = new ClientVoiceSearchService(
            factory,
            new StubClients(Clients),
            Options.Create(new OllamaSettings { BaseUrl = "http://localhost:11434", Model = "test" }),
            Options.Create(new DeepSeekSettings { ApiKey = "test-key", Model = "deepseek-flash" }),
            NullLogger<ClientVoiceSearchService>.Instance);

        var candidates = await service.SearchAsync("найди клиента алишер белоречка", default);

        Assert.Equal(33, candidates[0].Id);
        Assert.Equal("alias", candidates[0].MatchedField);
    }

    private static ClientVoiceSearchService Create(string? body)
    {
        HttpMessageHandler handler = body is null
            ? new FailingHandler()
            : new StubHandler(JsonSerializer.Serialize(new { response = body }));

        var factory = new StubFactory(new HttpClient(handler));
        var settings = Options.Create(new OllamaSettings { BaseUrl = "http://localhost:11434", Model = "test" });

        return new ClientVoiceSearchService(
            factory,
            new StubClients(Clients),
            settings,
            Options.Create(new DeepSeekSettings { ApiKey = string.Empty }),
            NullLogger<ClientVoiceSearchService>.Instance);
    }

    private sealed class StubClients(IReadOnlyList<КлиентDto> clients) : IКлиентRepository
    {
        public Task<IReadOnlyList<КлиентDto>> GetAllAsync(
            string? phone, string? alias, string? city, string? query, string sort, bool descending, bool activeOnly) =>
            Task.FromResult(clients);

        public Task<КлиентDto?> GetByIdAsync(int id) => Task.FromResult<КлиентDto?>(null);

        public Task<КлиентSummaryDto?> GetSummaryAsync(int clientId) =>
            Task.FromResult<КлиентSummaryDto?>(null);
    }

    private sealed class StubFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class NamedFactory(IReadOnlyDictionary<string, HttpClient> clients) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => clients[name];
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Ollama недоступна");
    }
}
