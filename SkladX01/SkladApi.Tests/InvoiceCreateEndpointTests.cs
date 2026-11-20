using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using SkladDB.Data;
using SkladDB.Models;
using SkladDB.Services;
using Xunit;

namespace SkladApi.Tests;

public class InvoiceCreateEndpointTests
{
    private static readonly Guid DealId =
        Guid.Parse("F0000000-0000-0000-0000-000001202310");

    [Fact]
    public async Task Creates_invoice_and_returns_201()
    {
        var invoice = new ОтпущенныйТоварDto
        {
            Id = 501,
            Номер = "О-ABC123",
            Дата = new DateTime(2026, 11, 19),
            Сумма = 701m,
            СделкаId = DealId,
        };

        var (controller, repository) = Create(
            new СозданиеНакладнойРезультат(СозданиеНакладнойСтатус.Успех, invoice));

        var result = await controller.CreateInvoice(
            DealId,
            ValidDto(),
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.Same(invoice, created.Value);

        Assert.Equal(DealId, repository.LastDealId);
        Assert.Equal("Отгрузка со склада", repository.LastComment);
        Assert.Single(repository.LastRows!);
        Assert.Equal(139, repository.LastRows![0].НоменклатураId);
        Assert.Equal(2m, repository.LastRows![0].Количество);
        Assert.Equal(350.5m, repository.LastRows![0].Цена);
    }

    [Fact]
    public async Task Rejects_invoice_without_rows()
    {
        var (controller, repository) = Create();

        var result = await controller.CreateInvoice(
            DealId,
            new СоздатьНакладнуюDto { Строки = [] },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(repository.LastDealId);
    }

    [Fact]
    public async Task Rejects_zero_quantity()
    {
        var (controller, _) = Create();

        var result = await controller.CreateInvoice(
            DealId,
            DtoWith(quantity: 0m, price: 10m),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Rejects_negative_price()
    {
        var (controller, _) = Create();

        var result = await controller.CreateInvoice(
            DealId,
            DtoWith(quantity: 1m, price: -1m),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Rejects_too_long_comment()
    {
        var (controller, _) = Create();

        var result = await controller.CreateInvoice(
            DealId,
            new СоздатьНакладнуюDto
            {
                Комментарий = new string('x', 501),
                Строки = [new СоздатьСтрокуНакладнойDto
                {
                    НоменклатураId = 139,
                    Количество = 1m,
                    Цена = 10m,
                }],
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Returns_not_found_for_unknown_deal()
    {
        var (controller, _) = Create(
            new СозданиеНакладнойРезультат(СозданиеНакладнойСтатус.СделкаНеНайдена));

        var result = await controller.CreateInvoice(
            DealId,
            ValidDto(),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Returns_bad_request_for_unknown_nomenclature()
    {
        var (controller, _) = Create(new СозданиеНакладнойРезультат(
            СозданиеНакладнойСтатус.НоменклатураНеНайдена,
            НоменклатураId: 999));

        var result = await controller.CreateInvoice(
            DealId,
            ValidDto(),
            CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("999", bad.Value?.ToString());
    }

    [Fact]
    public async Task Returns_service_unavailable_without_warehouse()
    {
        var (controller, _) = Create(
            new СозданиеНакладнойРезультат(СозданиеНакладнойСтатус.НетСклада));

        var result = await controller.CreateInvoice(
            DealId,
            ValidDto(),
            CancellationToken.None);

        var unavailable = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, unavailable.StatusCode);
    }

    private static СоздатьНакладнуюDto ValidDto() => new()
    {
        Дата = new DateTime(2026, 11, 19),
        Комментарий = "Отгрузка со склада",
        Строки =
        [
            new СоздатьСтрокуНакладнойDto
            {
                НоменклатураId = 139,
                Количество = 2m,
                Цена = 350.5m,
            },
        ],
    };

    private static СоздатьНакладнуюDto DtoWith(decimal quantity, decimal price) => new()
    {
        Строки =
        [
            new СоздатьСтрокуНакладнойDto
            {
                НоменклатураId = 139,
                Количество = quantity,
                Цена = price,
            },
        ],
    };

    private static (КлиентController Controller, StubInvoiceRepository Repository) Create(
        СозданиеНакладнойРезультат? result = null)
    {
        var repository = new StubInvoiceRepository(
            result ?? new СозданиеНакладнойРезультат(СозданиеНакладнойСтатус.Успех));

        var options = new DbContextOptionsBuilder<SkladContext>()
            .UseInMemoryDatabase($"invoice-create-{Guid.NewGuid()}")
            .Options;

        var context = new SkladContext(options, new FakeCurrentUserService());

        var controller = new КлиентController(
            new FakeКлиентRepository(),
            new FakeМеткаРасчетRepository(),
            repository,
            new FakeПринятыеДеньгиRepository(),
            new FakeVoiceSearch(),
            new NoopAuditService(),
            context,
            NullLogger<КлиентController>.Instance);

        return (controller, repository);
    }

    private sealed class StubInvoiceRepository(
        СозданиеНакладнойРезультат result) : IОтпущенныйТоварRepository
    {
        public Guid? LastDealId { get; private set; }
        public DateTime? LastDate { get; private set; }
        public string? LastComment { get; private set; }
        public IReadOnlyList<СоздатьСтрокуНакладнойDto>? LastRows { get; private set; }

        public Task<СозданиеНакладнойРезультат> CreateAsync(
            Guid dealId,
            DateTime? date,
            string? comment,
            IReadOnlyList<СоздатьСтрокуНакладнойDto> rows,
            CancellationToken cancellationToken = default)
        {
            LastDealId = dealId;
            LastDate = date;
            LastComment = comment;
            LastRows = rows;

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ОтпущенныйТоварDto>> GetByDealIdAsync(Guid dealId) =>
            Task.FromResult<IReadOnlyList<ОтпущенныйТоварDto>>([]);

        public Task<bool> InvoiceBelongsToDealAsync(
            Guid dealId,
            int invoiceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<СтрокаНакладнойDto>> GetRowsAsync(
            int invoiceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<СтрокаНакладнойDto>>([]);
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
    }

    private sealed class NoopAuditService : IAuditService
    {
        public Task LogChangeAsync(
            string entityType,
            long entityId,
            string action,
            object? oldValue,
            object? newValue,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeКлиентRepository : IКлиентRepository
    {
        public Task<IReadOnlyList<КлиентDto>> GetAllAsync(
            string? phone,
            string? alias,
            string? city,
            string? query,
            string sort,
            bool descending,
            bool activeOnly) =>
            Task.FromResult<IReadOnlyList<КлиентDto>>([]);

        public Task<КлиентDto?> GetByIdAsync(int id) =>
            Task.FromResult<КлиентDto?>(null);

        public Task<КлиентSummaryDto?> GetSummaryAsync(int clientId) =>
            Task.FromResult<КлиентSummaryDto?>(null);
    }

    private sealed class FakeМеткаРасчетRepository : IMеткаРасчетRepository
    {
        public Task<IReadOnlyList<МеткаРасчетDto>> GetByClientIdAsync(int clientId) =>
            Task.FromResult<IReadOnlyList<МеткаРасчетDto>>([]);

        public Task<МеткаРасчетDto> CreateAsync(
            int clientId,
            DateTime? date,
            string? comment) =>
            Task.FromResult(new МеткаРасчетDto());
    }

    private sealed class FakeПринятыеДеньгиRepository : IПринятыеДеньгиRepository
    {
        public Task<IReadOnlyList<ПринятыеДеньгиDto>> GetByDealIdAsync(Guid dealId) =>
            Task.FromResult<IReadOnlyList<ПринятыеДеньгиDto>>([]);

        public Task<ПринятыеДеньгиDto?> GetByIdAsync(
            Guid dealId,
            int paymentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ПринятыеДеньгиDto?>(null);

        public Task<СозданиеПлатежаРезультат> CreateAsync(
            Guid dealId,
            СоздатьПлатежDto dto,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new СозданиеПлатежаРезультат(
                СозданиеПлатежаСтатус.СделкаНеНайдена));
    }

    private sealed class FakeVoiceSearch : IClientVoiceSearchService
    {
        public Task<IReadOnlyList<ClientCandidate>> SearchAsync(
            string text,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClientCandidate>>([]);
    }
}
