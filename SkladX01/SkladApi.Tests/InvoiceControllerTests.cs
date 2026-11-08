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

public class InvoiceControllerTests
{
    private static readonly НоменклатураDto[] Products =
    [
        new() { Id = 139, Название = "Шайба Цинк_10" },
        new() { Id = 136, Название = "Шайба Чрн._6" },
        new() { Id = 123, Название = "Саморезы_3,5 х 25" },
    ];

    [Fact]
    public async Task Matches_known_product_and_keeps_quantity()
    {
        var controller = Create(Items(
            """{"raw_name":"шайба цинк 10","quantity":100,"unit":"штук"}"""));

        var result = await controller.ParseVoice(
            new VoiceParseRequest { RawText = "шайба цинк 10, 100 штук" }, CancellationToken.None);

        var rows = AssertRows(result);
        var row = Assert.Single(rows);
        Assert.Equal(139, row.ProductId);
        Assert.Equal(100, row.Quantity);
        Assert.Equal("штук", row.Unit);
        Assert.Equal("Шайба Цинк_10", row.DbName);
    }

    [Fact]
    public async Task Marks_unknown_product_as_not_found()
    {
        var controller = Create(Items(
            """{"raw_name":"гвозди 2,5х50","quantity":3,"unit":"кг"}"""));

        var result = await controller.ParseVoice(
            new VoiceParseRequest { RawText = "гвозди 2,5х50, 3 кг" }, CancellationToken.None);

        var row = Assert.Single(AssertRows(result));
        Assert.Null(row.ProductId);
        Assert.Equal("НЕ НАЙДЕНО В БД", row.DbName);
        Assert.Equal("гвозди 2,5х50", row.VoiceName);
        Assert.Equal(0, row.MatchConfidence);
    }

    [Fact]
    public async Task Accepts_plain_array_from_model()
    {
        var modelJson =
            """[{"raw_name":"саморезы 3,5 х 25","quantity":200,"unit":"штук"}]""";
        var controller = Create(JsonSerializer.Serialize(new { response = modelJson }));

        var result = await controller.ParseVoice(
            new VoiceParseRequest { RawText = "саморезы 3,5 х 25, 200 штук" }, CancellationToken.None);

        var row = Assert.Single(AssertRows(result));
        Assert.Equal(123, row.ProductId);
        Assert.Equal(200, row.Quantity);
    }

    [Fact]
    public async Task Returns_502_when_model_returns_garbage()
    {
        var controller = Create("это не JSON");

        var result = await controller.ParseVoice(
            new VoiceParseRequest { RawText = "шайба 10" }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }

    [Fact]
    public async Task Returns_400_for_empty_text()
    {
        var controller = Create(Items());

        var result = await controller.ParseVoice(
            new VoiceParseRequest { RawText = "   " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static string Items(params string[] items)
    {
        var modelJson = $$"""{"items":[{{string.Join(',', items)}}]}""";

        return JsonSerializer.Serialize(new { response = modelJson });
    }

    private static IReadOnlyList<ProcessedInvoiceRow> AssertRows(
        ActionResult<IReadOnlyList<ProcessedInvoiceRow>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);

        return Assert.IsAssignableFrom<IReadOnlyList<ProcessedInvoiceRow>>(ok.Value);
    }

    private static InvoiceController Create(string ollamaBody)
    {
        var client = new HttpClient(new StubHandler(ollamaBody));
        var factory = new StubFactory(client);
        var settings = Options.Create(new OllamaSettings
        {
            BaseUrl = "http://localhost:11434",
            Model = "test",
        });

        return new InvoiceController(
            factory,
            new StubRepository(Products),
            settings,
            NullLogger<InvoiceController>.Instance);
    }

    private sealed class StubRepository(IEnumerable<НоменклатураDto> items) : IНоменклатураRepository
    {
        private readonly IReadOnlyList<НоменклатураDto> _items = items.ToList();

        public Task<IReadOnlyList<НоменклатураDto>> GetActiveAsync() => Task.FromResult(_items);
    }

    private sealed class StubFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
