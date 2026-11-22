using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using Xunit;

namespace SkladApi.Tests;

public class SupplierReceiptCreateEndpointTests
{
    private static readonly Guid DealId = Guid.Parse("F0000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Creates_receipt_and_returns_201()
    {
        var created = new ПолученныйТоварDto
        {
            Id = Guid.NewGuid(),
            Номер = "П-ABC123",
            Дата = new DateTime(2026, 11, 22),
            Сумма = 1000,
            СделкаId = DealId,
        };
        var repository = new StubRepository(
            new СозданиеПриходаРезультат(СозданиеПриходаСтатус.Успех, created));
        var controller = Create(repository);

        var dto = new СоздатьПриходDto
        {
            Строки =
            [
                new СоздатьСтрокуПриходаDto
                {
                    НоменклатураId = 49,
                    Количество = 2,
                    Цена = 500,
                },
            ],
        };

        var result = await controller.CreateReceipt(DealId, dto, default);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        var receipt = Assert.IsType<ПолученныйТоварDto>(objectResult.Value);
        Assert.Equal("П-ABC123", receipt.Номер);
        Assert.Equal(DealId, repository.LastDealId);
    }

    [Fact]
    public async Task Returns_404_when_deal_not_found()
    {
        var repository = new StubRepository(
            new СозданиеПриходаРезультат(СозданиеПриходаСтатус.СделкаНеНайдена));
        var controller = Create(repository);

        var result = await controller.CreateReceipt(DealId, ValidDto(), default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Returns_400_when_nomenclature_missing()
    {
        var repository = new StubRepository(
            new СозданиеПриходаРезультат(
                СозданиеПриходаСтатус.НоменклатураНеНайдена, НоменклатураId: 99));
        var controller = Create(repository);

        var result = await controller.CreateReceipt(DealId, ValidDto(), default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("99", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task Rejects_empty_rows()
    {
        var controller = Create(new StubRepository(
            new СозданиеПриходаРезультат(СозданиеПриходаСтатус.Успех)));

        var result = await controller.CreateReceipt(DealId, new СоздатьПриходDto(), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Rejects_non_positive_quantity()
    {
        var controller = Create(new StubRepository(
            new СозданиеПриходаРезультат(СозданиеПриходаСтатус.Успех)));

        var dto = new СоздатьПриходDto
        {
            Строки =
            [
                new СоздатьСтрокуПриходаDto
                {
                    НоменклатураId = 1,
                    Количество = 0,
                    Цена = 10,
                },
            ],
        };

        var result = await controller.CreateReceipt(DealId, dto, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static СоздатьПриходDto ValidDto() =>
        new()
        {
            Строки =
            [
                new СоздатьСтрокуПриходаDto
                {
                    НоменклатураId = 49,
                    Количество = 1,
                    Цена = 100,
                },
            ],
        };

    private static ПоставщикController Create(IПолученныйТоварRepository repository) =>
        new(null!, null!, repository, null!, null!, null!);

    private sealed class StubRepository(СозданиеПриходаРезультат result)
        : IПолученныйТоварRepository
    {
        public Guid? LastDealId { get; private set; }

        public Task<IReadOnlyList<ПолученныйТоварDto>> GetByDealIdAsync(Guid dealId) =>
            Task.FromResult<IReadOnlyList<ПолученныйТоварDto>>([]);

        public Task<bool> ReceiptBelongsToDealAsync(
            Guid dealId,
            Guid receiptId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<СтрокаПриходаDto>> GetRowsAsync(
            Guid receiptId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<СтрокаПриходаDto>>([]);

        public Task<СозданиеПриходаРезультат> CreateAsync(
            Guid dealId,
            DateTime? date,
            string? comment,
            IReadOnlyList<СоздатьСтрокуПриходаDto> rows,
            CancellationToken cancellationToken = default)
        {
            LastDealId = dealId;

            return Task.FromResult(result);
        }
    }
}
