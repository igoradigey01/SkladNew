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

public class NomenclatureCreateTests
{
    [Fact]
    public void Normalize_drops_empty_values()
    {
        // «— не выбрано —» в UI превращается в пустую строку; такой строки в БД
        // быть не должно, иначе триггер отклоняет List-характеристику.
        var rows = НоменклатураRepository.NormalizeCharacteristics(
        [
            new ЗначениеХарактеристикиRequest { ХарактеристикаId = 7, Значение = "16" },
            new ЗначениеХарактеристикиRequest { ХарактеристикаId = 24, Значение = "" },
            new ЗначениеХарактеристикиRequest { ХарактеристикаId = 25, Значение = "   " },
        ]);

        Assert.Single(rows);
        Assert.Equal(7, rows[0].ХарактеристикаId);
        Assert.Equal("16", rows[0].Значение);
    }

    [Fact]
    public void Normalize_trims_and_keeps_last_duplicate()
    {
        var rows = НоменклатураRepository.NormalizeCharacteristics(
        [
            new ЗначениеХарактеристикиRequest { ХарактеристикаId = 7, Значение = " 12 " },
            new ЗначениеХарактеристикиRequest { ХарактеристикаId = 7, Значение = " 16 " },
        ]);

        Assert.Single(rows);
        Assert.Equal("16", rows[0].Значение);
    }

    [Fact]
    public async Task Create_passes_optional_characteristic_through_only_when_filled()
    {
        var repository = new StubRepository(Характеристики());
        var controller = CreateController(repository);

        var result = await controller.Create(new СоздатьНоменклатуруRequest
        {
            Код = "НМ-00200",
            Название = "ДСП \"Сатин\"(Шампань) 2800 x 2070 x 16 мм",
            ТипId = 1,
            ВидНоменклатурыId = 5,
            БазоваяЕдИзмId = 5,
            Характеристики =
            [
                new ЗначениеХарактеристикиRequest { ХарактеристикаId = 7, Значение = "16" },
                new ЗначениеХарактеристикиRequest { ХарактеристикаId = 24, Значение = "" },
            ],
        });

        Assert.IsType<CreatedResult>(result);
        Assert.NotNull(repository.LastRequest);

        var normalized = НоменклатураRepository.NormalizeCharacteristics(
            repository.LastRequest!.Характеристики);

        Assert.Single(normalized);
        Assert.Equal("16", normalized[0].Значение);
    }

    [Fact]
    public async Task Create_rejects_duplicate_characteristics()
    {
        var controller = CreateController(new StubRepository(Характеристики()));

        var result = await controller.Create(new СоздатьНоменклатуруRequest
        {
            Код = "НМ-00201",
            Название = "ДСП",
            ТипId = 1,
            ВидНоменклатурыId = 5,
            БазоваяЕдИзмId = 5,
            Характеристики =
            [
                new ЗначениеХарактеристикиRequest { ХарактеристикаId = 7, Значение = "16" },
                new ЗначениеХарактеристикиRequest { ХарактеристикаId = 7, Значение = "18" },
            ],
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(bad.Value));
        var details = body.RootElement.GetProperty("details")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToList();

        Assert.Contains(details, item => item!.Contains("дважды"));
    }

    [Fact]
    public async Task Create_rejects_name_longer_than_column()
    {
        var controller = CreateController(new StubRepository(Характеристики()));

        var result = await controller.Create(new СоздатьНоменклатуруRequest
        {
            Код = "НМ-00202",
            Название = new string('Я', 201),
            ТипId = 1,
            БазоваяЕдИзмId = 5,
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(bad.Value));
        var details = body.RootElement.GetProperty("details")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToList();

        Assert.Contains(details, item => item!.Contains("200"));
    }

    [Fact]
    public async Task Create_requires_mandatory_characteristic()
    {
        var controller = CreateController(new StubRepository(Характеристики()));

        var result = await controller.Create(new СоздатьНоменклатуруRequest
        {
            Код = "НМ-00203",
            Название = "ДСП",
            ТипId = 1,
            ВидНоменклатурыId = 5,
            БазоваяЕдИзмId = 5,
            Характеристики =
            [
                new ЗначениеХарактеристикиRequest { ХарактеристикаId = 7, Значение = "  " },
            ],
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(bad.Value));
        var details = body.RootElement.GetProperty("details")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToList();

        Assert.Contains(details, item => item!.Contains("Толщина"));
    }

    private static IReadOnlyList<ХарактеристикаВидаDto> Характеристики() =>
    [
        new()
        {
            Id = 7,
            Название = "Толщина",
            ТипЗначения = "Number",
            Обязательный = true,
            Минимум = 5,
            Максимум = 50,
        },
        new()
        {
            Id = 24,
            Название = "Цвет кромки",
            ТипЗначения = "List",
            Обязательный = false,
            СписокЗначений = ["Белый", "Дуб"],
        },
    ];

    private static NomenclatureController CreateController(
        IНоменклатураRepository repository)
    {
        var embeddings = new OllamaEmbeddingService(
            new HttpClient(),
            Options.Create(new OllamaSettings()));

        return new NomenclatureController(
            repository,
            embeddings,
            NullLogger<NomenclatureController>.Instance);
    }

    private sealed class StubRepository(IReadOnlyList<ХарактеристикаВидаDto> characteristics)
        : IНоменклатураRepository
    {
        public СоздатьНоменклатуруRequest? LastRequest { get; private set; }

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

        public Task<string> GetNextCodeAsync() => Task.FromResult("НМ-0042");

        public Task<НоменклатураReferencesDto> GetReferencesAsync() =>
            Task.FromResult(new НоменклатураReferencesDto());

        public Task<IReadOnlyList<ХарактеристикаВидаDto>> GetCharacteristicsAsync(int видId) =>
            Task.FromResult(characteristics);

        public Task<int> CreateAsync(СоздатьНоменклатуруRequest request)
        {
            LastRequest = request;

            return Task.FromResult(206);
        }
    }
}
