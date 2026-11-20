using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using SkladDB.Data;
using SkladDB.Services;
using Xunit;

namespace SkladApi.Tests;

public class PaymentCreateEndpointTests
{
    private static readonly Guid DealId =
        Guid.Parse("E0000000-0000-0000-0000-000000000042");

    [Fact]
    public async Task Creates_payment_and_returns_201()
    {
        var payment = new ПринятыеДеньгиDto
        {
            Id = 777,
            Дата = new DateTime(2026, 11, 19),
            Сумма = 1250.5m,
            СделкаId = DealId,
            СпособОплаты = 2,
            НомерДокумента = "ПКО-15",
            Комментарий = "Оплата по счёту",
        };

        var (controller, repository) = Create(
            new СозданиеПлатежаРезультат(СозданиеПлатежаСтатус.Успех, payment));

        var dto = new СоздатьПлатежDto
        {
            Дата = new DateTime(2026, 11, 19),
            Сумма = 1250.5m,
            СпособОплаты = 2,
            НомерДокумента = "ПКО-15",
            Комментарий = "Оплата по счёту",
        };

        var result = await controller.CreatePayment(DealId, dto, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.Same(payment, created.Value);

        Assert.Equal(DealId, repository.LastDealId);
        Assert.Same(dto, repository.LastDto);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Rejects_non_positive_sum(decimal sum)
    {
        var (controller, repository) = Create();

        var result = await controller.CreatePayment(
            DealId,
            new СоздатьПлатежDto { Сумма = sum },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(repository.LastDealId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Rejects_unknown_payment_method(int method)
    {
        var (controller, repository) = Create();

        var result = await controller.CreatePayment(
            DealId,
            new СоздатьПлатежDto { Сумма = 100m, СпособОплаты = method },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(repository.LastDealId);
    }

    [Fact]
    public async Task Rejects_too_long_document_number()
    {
        var (controller, _) = Create();

        var result = await controller.CreatePayment(
            DealId,
            new СоздатьПлатежDto
            {
                Сумма = 100m,
                НомерДокумента = new string('9', 31),
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Rejects_too_long_comment()
    {
        var (controller, _) = Create();

        var result = await controller.CreatePayment(
            DealId,
            new СоздатьПлатежDto
            {
                Сумма = 100m,
                Комментарий = new string('x', 501),
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Returns_not_found_for_unknown_deal()
    {
        var (controller, _) = Create(
            new СозданиеПлатежаРезультат(СозданиеПлатежаСтатус.СделкаНеНайдена));

        var result = await controller.CreatePayment(
            DealId,
            new СоздатьПлатежDto { Сумма = 100m },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static (КлиентController Controller, StubPaymentRepository Repository) Create(
        СозданиеПлатежаРезультат? result = null)
    {
        var repository = new StubPaymentRepository(
            result ?? new СозданиеПлатежаРезультат(СозданиеПлатежаСтатус.Успех));

        var options = new DbContextOptionsBuilder<SkladContext>()
            .UseInMemoryDatabase($"payment-create-{Guid.NewGuid()}")
            .Options;

        var context = new SkladContext(options, new FakeCurrentUserService());

        var controller = new КлиентController(
            new FakeКлиентRepository(),
            new FakeМеткаРасчетRepository(),
            new FakeInvoiceRepository(),
            repository,
            new FakeVoiceSearch(),
            new NoopAuditService(),
            context,
            NullLogger<КлиентController>.Instance);

        return (controller, repository);
    }

    private sealed class StubPaymentRepository(
        СозданиеПлатежаРезультат result) : IПринятыеДеньгиRepository
    {
        public Guid? LastDealId { get; private set; }
        public СоздатьПлатежDto? LastDto { get; private set; }

        public Task<СозданиеПлатежаРезультат> CreateAsync(
            Guid dealId,
            СоздатьПлатежDto dto,
            CancellationToken cancellationToken = default)
        {
            LastDealId = dealId;
            LastDto = dto;

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ПринятыеДеньгиDto>> GetByDealIdAsync(Guid dealId) =>
            Task.FromResult<IReadOnlyList<ПринятыеДеньгиDto>>([]);

        public Task<ПринятыеДеньгиDto?> GetByIdAsync(
            Guid dealId,
            int paymentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ПринятыеДеньгиDto?>(null);
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

    private sealed class FakeInvoiceRepository : IОтпущенныйТоварRepository
    {
        public Task<СозданиеНакладнойРезультат> CreateAsync(
            Guid dealId,
            DateTime? date,
            string? comment,
            IReadOnlyList<СоздатьСтрокуНакладнойDto> rows,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new СозданиеНакладнойРезультат(
                СозданиеНакладнойСтатус.СделкаНеНайдена));

        public Task<IReadOnlyList<ОтпущенныйТоварDto>> GetByDealIdAsync(Guid dealId) =>
            Task.FromResult<IReadOnlyList<ОтпущенныйТоварDto>>([]);

        public Task<bool> InvoiceBelongsToDealAsync(
            Guid dealId,
            int invoiceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<СтрокаНакладнойDto>> GetRowsAsync(
            int invoiceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<СтрокаНакладнойDto>>([]);
    }

    private sealed class FakeVoiceSearch : IClientVoiceSearchService
    {
        public Task<IReadOnlyList<ClientCandidate>> SearchAsync(
            string text,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClientCandidate>>([]);
    }
}
