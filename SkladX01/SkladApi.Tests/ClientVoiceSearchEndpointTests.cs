using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class ClientVoiceSearchEndpointTests
{
    [Fact]
    public async Task Rejects_empty_text()
    {
        var controller = Create();

        var result = await controller.SearchClientsByVoice(
            new ClientVoiceQuery { RawText = "   " }, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Rejects_text_longer_than_limit()
    {
        var controller = Create();

        var result = await controller.SearchClientsByVoice(
            new ClientVoiceQuery { RawText = new string('я', ClientVoiceSearchService.MaxTextLength + 1) },
            default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Returns_candidates_from_service()
    {
        var candidates = new List<ClientCandidate>
        {
            new() { Id = 33, Alias = "Алишер  Белоречка", Score = 100, MatchedField = "alias" },
        };
        var controller = Create(candidates);

        var result = await controller.SearchClientsByVoice(
            new ClientVoiceQuery { RawText = "алишер белоречка" }, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var actual = Assert.IsAssignableFrom<IReadOnlyList<ClientCandidate>>(ok.Value);
        Assert.Equal(33, actual[0].Id);
    }

    [Fact]
    public void Voice_search_route_lives_in_clients_controller()
    {
        var method = typeof(КлиентController)
            .GetMethod(nameof(КлиентController.SearchClientsByVoice));

        Assert.NotNull(method);

        var post = method.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(post);
        Assert.Equal("voice-search", post.Template);

        var route = typeof(КлиентController).GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("api/clients", route.Template);
    }

    private static КлиентController Create(IReadOnlyList<ClientCandidate>? candidates = null) =>
        new(
            new StubClients(),
            // В этих действиях репозитории сделок не используются.
            null!,
            null!,
            null!,
            new StubVoiceSearch(candidates ?? []));

    private sealed class StubClients : IКлиентRepository
    {
        public Task<IReadOnlyList<КлиентDto>> GetAllAsync(
            string? phone, string? alias, string? city, string? query, string sort, bool descending, bool activeOnly) =>
            Task.FromResult<IReadOnlyList<КлиентDto>>([]);

        public Task<КлиентDto?> GetByIdAsync(int id) => Task.FromResult<КлиентDto?>(null);

        public Task<КлиентSummaryDto?> GetSummaryAsync(int clientId) =>
            Task.FromResult<КлиентSummaryDto?>(null);
    }

    private sealed class StubVoiceSearch(IReadOnlyList<ClientCandidate> candidates)
        : IClientVoiceSearchService
    {
        public Task<IReadOnlyList<ClientCandidate>> SearchAsync(
            string text, CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }
}
