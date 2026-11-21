using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class NomenclatureNextCodeTests
{
    [Fact]
    public void Formats_first_code_when_table_is_empty()
    {
        Assert.Equal("НМ-00001", НоменклатураRepository.FormatNextCode(null));
    }

    [Fact]
    public void Formats_next_code_after_existing()
    {
        Assert.Equal("НМ-00142", НоменклатураRepository.FormatNextCode(141));
    }

    [Fact]
    public async Task Controller_returns_next_code()
    {
        var controller = CreateController("НМ-00042");

        var result = await controller.NextCode();

        var ok = Assert.IsType<OkObjectResult>(result);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal("НМ-00042", body.RootElement.GetProperty("код").GetString());
    }

    private static NomenclatureController CreateController(string nextCode)
    {
        var embeddings = new OllamaEmbeddingService(
            new HttpClient(),
            Options.Create(new OllamaSettings()));

        return new NomenclatureController(
            new StubRepository(nextCode),
            embeddings,
            NullLogger<NomenclatureController>.Instance);
    }

    private sealed class StubRepository(string nextCode) : IНоменклатураRepository
    {
        public Task<IReadOnlyList<НоменклатураDto>> GetActiveAsync() =>
            Task.FromResult<IReadOnlyList<НоменклатураDto>>([]);

        public Task EnsureVectorColumnAsync() => Task.CompletedTask;

        public Task<IReadOnlyList<НоменклатураВекторDto>> GetWithoutVectorAsync() =>
            Task.FromResult<IReadOnlyList<НоменклатураВекторDto>>([]);

        public Task<IReadOnlyList<НоменклатураВекторDto>> GetWithVectorAsync() =>
            Task.FromResult<IReadOnlyList<НоменклатураВекторDto>>([]);

        public Task UpdateVectorAsync(int id, byte[] vector) => Task.CompletedTask;

        public Task<IReadOnlyList<НоменклатураQuickDto>> QuickSearchAsync(
            string query, int limit) =>
            Task.FromResult<IReadOnlyList<НоменклатураQuickDto>>([]);

        public Task<string> GetNextCodeAsync() => Task.FromResult(nextCode);

        public Task<НоменклатураReferencesDto> GetReferencesAsync() =>
            Task.FromResult(new НоменклатураReferencesDto());

        public Task<IReadOnlyList<ХарактеристикаВидаDto>> GetCharacteristicsAsync(
            int видId) =>
            Task.FromResult<IReadOnlyList<ХарактеристикаВидаDto>>([]);

        public Task<int> CreateAsync(СоздатьНоменклатуруRequest request) =>
            Task.FromResult(0);
    }
}
