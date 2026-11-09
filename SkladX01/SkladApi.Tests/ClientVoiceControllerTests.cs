using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class ClientVoiceControllerTests
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
        var controller = Create("""{"name":"алишер белоречка","city":"","phone":""}""");

        var result = await controller.Search(new ClientVoiceQuery { RawText = "найди клиента алишер белоречка" }, default);

        var candidates = Assert.IsAssignableFrom<IReadOnlyList<ClientCandidate>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(33, candidates[0].Id);
        Assert.Equal(100, candidates[0].Score);
        Assert.Equal("alias", candidates[0].MatchedField);
    }

    [Fact]
    public async Task Finds_client_by_city_in_case()
    {
        var controller = Create("""{"name":"","city":"Родники","phone":""}""");

        var result = await controller.Search(new ClientVoiceQuery { RawText = "клиент из родников" }, default);

        var candidates = Assert.IsAssignableFrom<IReadOnlyList<ClientCandidate>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(33, candidates[0].Id);
        Assert.Equal("city", candidates[0].MatchedField);
    }

    [Fact]
    public async Task Parses_phone_spoken_as_words_when_model_returns_nothing()
    {
        var controller = Create("""{"name":"","city":"","phone":""}""");

        var result = await controller.Search(
            new ClientVoiceQuery { RawText = "восемь девять один восемь два пять ноль семь шесть семь один" },
            default);

        var candidates = Assert.IsAssignableFrom<IReadOnlyList<ClientCandidate>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(33, candidates[0].Id);
        Assert.Equal("phone", candidates[0].MatchedField);
        Assert.Equal(100, candidates[0].Score);
    }

    [Fact]
    public async Task Falls_back_to_text_search_when_model_is_down()
    {
        var controller = Create(body: null);

        var result = await controller.Search(new ClientVoiceQuery { RawText = "алишер белоречка" }, default);

        var candidates = Assert.IsAssignableFrom<IReadOnlyList<ClientCandidate>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(33, candidates[0].Id);
    }

    private static ClientVoiceController Create(string? body)
    {
        HttpMessageHandler handler = body is null
            ? new FailingHandler()
            : new StubHandler(JsonSerializer.Serialize(new { response = body }));

        var factory = new StubFactory(new HttpClient(handler));
        var settings = Options.Create(new OllamaSettings { BaseUrl = "http://localhost:11434", Model = "test" });

        return new ClientVoiceController(
            factory,
            new StubClients(Clients),
            settings,
            NullLogger<ClientVoiceController>.Instance);
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
