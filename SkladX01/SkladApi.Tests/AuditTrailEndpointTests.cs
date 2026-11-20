using System.Text.Json;
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

/// <summary>
/// Проверки гибридного контура: старые значения читаются репозиторием (в бою —
/// Dapper), запись идёт через EF Core, история — отдельным Dapper-запросом.
/// </summary>
public class AuditTrailEndpointTests
{
    private static readonly Guid DealId =
        Guid.Parse("F0000000-0000-0000-0000-000001202310");

    private const int InvoiceId = 501;
    private const int PaymentId = 77;

    [Fact]
    public async Task Invoice_update_recalculates_sum_and_writes_audit()
    {
        var rows = new List<СтрокаНакладнойDto>
        {
            Row(11, quantity: 2m, price: 100m),
            Row(12, quantity: 1m, price: 50m),
        };

        var (controller, context, _) = Create(rows: rows);

        var result = await controller.UpdateInvoiceRows(
            DealId,
            InvoiceId,
            new ОбновитьНакладнуюDto
            {
                Строки =
                [
                    new() { Id = 11, Количество = 3m, Цена = 120.5m },
                ],
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);

        var changed = context.Расходы.Local.Single(x => x.Id == 11);
        Assert.Equal(3m, changed.Quantity);
        Assert.Equal(120.5m, changed.Price);
        Assert.Equal(361.5m, changed.Sum);

        var log = Assert.Single(context.AuditLogs.Local);
        Assert.Equal("Invoice", log.EntityType);
        Assert.Equal(InvoiceId, log.EntityId);
        Assert.Equal("Update", log.Action);
        Assert.Equal(1, log.ChangedByEmployeeId);
        Assert.Equal("Сидорова Мария Ивановна", log.ChangedByEmployeeName);
        Assert.Equal(DateTimeKind.Utc, log.Timestamp.Kind);

        using var oldDoc = JsonDocument.Parse(log.OldData!);
        var oldRow = oldDoc.RootElement.EnumerateArray().First(x => x.GetProperty("id").GetInt32() == 11);
        Assert.Equal(2m, oldRow.GetProperty("quantity").GetDecimal());
        Assert.Equal(200m, oldRow.GetProperty("sum").GetDecimal());

        using var newDoc = JsonDocument.Parse(log.NewData!);
        var newRows = newDoc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, newRows.Count);
        var newRow = newRows.First(x => x.GetProperty("id").GetInt32() == 11);
        Assert.Equal(3m, newRow.GetProperty("quantity").GetDecimal());
        Assert.Equal(361.5m, newRow.GetProperty("sum").GetDecimal());

        // Вторая строка не менялась и осталась в снимке как была.
        var untouched = newRows.First(x => x.GetProperty("id").GetInt32() == 12);
        Assert.Equal(50m, untouched.GetProperty("sum").GetDecimal());
    }

    [Fact]
    public async Task Invoice_update_without_changes_writes_nothing()
    {
        var rows = new List<СтрокаНакладнойDto> { Row(11, quantity: 2m, price: 100m) };
        var (controller, context, _) = Create(rows: rows);

        var result = await controller.UpdateInvoiceRows(
            DealId,
            InvoiceId,
            new ОбновитьНакладнуюDto
            {
                Строки = [new() { Id = 11, Количество = 2m, Цена = 100m }],
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(context.AuditLogs.Local);
        Assert.Empty(context.Расходы.Local);
    }

    [Fact]
    public async Task Invoice_update_rejects_unknown_invoice()
    {
        var (controller, _, _) = Create(invoiceBelongs: false);

        var result = await controller.UpdateInvoiceRows(
            DealId,
            InvoiceId,
            new ОбновитьНакладнуюDto { Строки = [new() { Id = 11, Количество = 1m, Цена = 1m }] },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Invoice_update_rejects_row_from_another_invoice()
    {
        var (controller, _, _) = Create();
        var result = await controller.UpdateInvoiceRows(
            DealId,
            InvoiceId,
            new ОбновитьНакладнуюDto { Строки = [new() { Id = 999, Количество = 1m, Цена = 1m }] },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, -0.01)]
    public async Task Invoice_update_validates_numbers(decimal quantity, decimal price)
    {
        var (controller, _, _) = Create();

        var result = await controller.UpdateInvoiceRows(
            DealId,
            InvoiceId,
            new ОбновитьНакладнуюDto { Строки = [new() { Id = 11, Количество = quantity, Цена = price }] },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Invoice_update_rejects_empty_request()
    {
        var (controller, _, _) = Create();

        var result = await controller.UpdateInvoiceRows(
            DealId, InvoiceId, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Invoice_rows_returns_rows_for_own_invoice()
    {
        var rows = new List<СтрокаНакладнойDto> { Row(11, quantity: 2m, price: 100m) };
        var (controller, _, _) = Create(rows: rows);

        var result = await controller.GetInvoiceRows(DealId, InvoiceId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<СтрокаНакладнойDto>>(ok.Value);
        Assert.Single(returned);
    }

    [Fact]
    public async Task Invoice_rows_returns_not_found_for_foreign_invoice()
    {
        var (controller, _, _) = Create(invoiceBelongs: false);

        var result = await controller.GetInvoiceRows(DealId, InvoiceId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Payment_update_writes_old_and_new_sum()
    {
        var (controller, context, _) = Create(
            payment: Payment(sum: 1000m));

        var result = await controller.UpdatePayment(
            DealId,
            PaymentId,
            new ОбновитьПлатежDto { Сумма = 1200m },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ПринятыеДеньгиDto>(ok.Value);
        Assert.Equal(1200m, dto.Сумма);

        var tracked = context.Деньги.Local.Single(x => x.Id == PaymentId);
        Assert.Equal(1200m, tracked.Sum);

        var log = Assert.Single(context.AuditLogs.Local);
        Assert.Equal("Payment", log.EntityType);
        Assert.Equal(PaymentId, log.EntityId);
        Assert.Equal(1000m, SumOf(log.OldData!));
        Assert.Equal(1200m, SumOf(log.NewData!));
    }

    [Fact]
    public async Task Payment_update_with_same_sum_writes_nothing()
    {
        var (controller, context, _) = Create(payment: Payment(sum: 1000m));

        var result = await controller.UpdatePayment(
            DealId, PaymentId, new ОбновитьПлатежDto { Сумма = 1000m }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(context.AuditLogs.Local);
    }

    [Fact]
    public async Task Payment_update_returns_not_found_for_unknown_payment()
    {
        var (controller, _, _) = Create(payment: null);

        var result = await controller.UpdatePayment(
            DealId, PaymentId, new ОбновитьПлатежDto { Сумма = 10m }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Payment_update_rejects_negative_sum()
    {
        var (controller, _, _) = Create(payment: Payment(sum: 1000m));

        var result = await controller.UpdatePayment(
            DealId, PaymentId, new ОбновитьПлатежDto { Сумма = -1m }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Employee_falls_back_to_system_when_mock_missing()
    {
        var (controller, context, _) = Create(
            payment: Payment(sum: 100m),
            employee: new EmployeeInfo(0, "Система"));

        await controller.UpdatePayment(
            DealId, PaymentId, new ОбновитьПлатежDto { Сумма = 200m }, CancellationToken.None);

        var log = Assert.Single(context.AuditLogs.Local);
        Assert.Equal("Система", log.ChangedByEmployeeName);
    }

    private static decimal SumOf(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("sum").GetDecimal();
    }

    private static СтрокаНакладнойDto Row(int id, decimal quantity, decimal price) => new()
    {
        Id = id,
        НоменклатураId = 100 + id,
        Номенклатура = $"Товар {id}",
        Количество = quantity,
        Цена = price,
        Сумма = Math.Round(quantity * price, 2, MidpointRounding.AwayFromZero),
    };

    private static ПринятыеДеньгиDto Payment(decimal sum) => new()
    {
        Id = PaymentId,
        Дата = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
        Сумма = sum,
        СделкаId = DealId,
    };

    private static (КлиентController Controller, SkladContext Context, FakeAuditLogRepository Audit)
        Create(
            List<СтрокаНакладнойDto>? rows = null,
            bool invoiceBelongs = true,
            ПринятыеДеньгиDto? payment = null,
            EmployeeInfo? employee = null)
    {
        var options = new DbContextOptionsBuilder<SkladContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}")
            .Options;

        // InMemory-провайдер не умеет обновлять «несуществующие» строки, поэтому
        // сначала наполняем базу отдельным контекстом — как в настоящей БД.
        Seed(options, rows ?? [], payment);

        var context = new SkladContext(options, new FakeCurrentUserService());
        var audit = new FakeAuditLogRepository();

        var controller = new КлиентController(
            new FakeКлиентRepository(),
            new FakeМеткаРасчетRepository(),
            new FakeОтпущенныйТоварRepository(rows ?? [], invoiceBelongs),
            new FakeПринятыеДеньгиRepository(payment),
            new FakeVoiceSearch(),
            new AuditService(context, new FakeEmployeeContext(
                employee ?? new EmployeeInfo(1, "Сидорова Мария Ивановна"))),
            context,
            NullLogger<КлиентController>.Instance);

        return (controller, context, audit);
    }

    private static void Seed(
        DbContextOptions<SkladContext> options,
        IReadOnlyList<СтрокаНакладнойDto> rows,
        ПринятыеДеньгиDto? payment)
    {
        using var seed = new SkladContext(options, new FakeCurrentUserService());

        foreach (var row in rows)
        {
            seed.Расходы.Add(new Расход
            {
                Id = row.Id,
                НакладнаяId = InvoiceId,
                КлиентId = 1,
                НоменклатураId = row.НоменклатураId,
                Quantity = row.Количество,
                Price = row.Цена,
                Sum = row.Сумма,
            });
        }

        if (payment is not null)
        {
            seed.Деньги.Add(new Деньги
            {
                Id = payment.Id,
                Date = payment.Дата,
                Sum = payment.Сумма,
                КодСделки = payment.СделкаId,
            });
        }

        seed.SaveChanges();
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
    }

    private sealed class FakeEmployeeContext : IEmployeeContext
    {
        private readonly EmployeeInfo _employee;

        public FakeEmployeeContext(EmployeeInfo employee) => _employee = employee;

        public Task<EmployeeInfo> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_employee);
    }

    private sealed class FakeОтпущенныйТоварRepository : IОтпущенныйТоварRepository
    {
        private readonly IReadOnlyList<СтрокаНакладнойDto> _rows;
        private readonly bool _belongs;

        public FakeОтпущенныйТоварRepository(
            IReadOnlyList<СтрокаНакладнойDto> rows,
            bool belongs)
        {
            _rows = rows;
            _belongs = belongs;
        }

        public Task<IReadOnlyList<ОтпущенныйТоварDto>> GetByDealIdAsync(Guid dealId) =>
            Task.FromResult<IReadOnlyList<ОтпущенныйТоварDto>>([]);

        public Task<bool> InvoiceBelongsToDealAsync(
            Guid dealId, int invoiceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_belongs);

        public Task<IReadOnlyList<СтрокаНакладнойDto>> GetRowsAsync(
            int invoiceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rows);

        public Task<СозданиеНакладнойРезультат> CreateAsync(
            Guid dealId,
            DateTime? date,
            string? comment,
            IReadOnlyList<СоздатьСтрокуНакладнойDto> rows,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new СозданиеНакладнойРезультат(
                СозданиеНакладнойСтатус.СделкаНеНайдена));
    }

    private sealed class FakeПринятыеДеньгиRepository : IПринятыеДеньгиRepository
    {
        private readonly ПринятыеДеньгиDto? _payment;

        public FakeПринятыеДеньгиRepository(ПринятыеДеньгиDto? payment) => _payment = payment;

        public Task<IReadOnlyList<ПринятыеДеньгиDto>> GetByDealIdAsync(Guid dealId) =>
            Task.FromResult<IReadOnlyList<ПринятыеДеньгиDto>>(
                _payment is null ? [] : [_payment]);

        public Task<ПринятыеДеньгиDto?> GetByIdAsync(
            Guid dealId, int paymentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_payment);

        public Task<СозданиеПлатежаРезультат> CreateAsync(
            Guid dealId,
            СоздатьПлатежDto dto,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new СозданиеПлатежаРезультат(
                СозданиеПлатежаСтатус.СделкаНеНайдена));
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public int? LastLimit { get; private set; }

        public Task<IReadOnlyList<AuditLogDto>> GetAsync(
            string entityType,
            long entityId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<AuditLogDto>>([]);
        }
    }

    private sealed class FakeКлиентRepository : IКлиентRepository
    {
        public Task<IReadOnlyList<КлиентDto>> GetAllAsync(
            string? phone, string? alias, string? city, string? query,
            string sort, bool descending, bool activeOnly) =>
            Task.FromResult<IReadOnlyList<КлиентDto>>([]);

        public Task<КлиентDto?> GetByIdAsync(int id) => Task.FromResult<КлиентDto?>(null);

        public Task<КлиентSummaryDto?> GetSummaryAsync(int clientId) =>
            Task.FromResult<КлиентSummaryDto?>(null);
    }

    private sealed class FakeМеткаРасчетRepository : IMеткаРасчетRepository
    {
        public Task<IReadOnlyList<МеткаРасчетDto>> GetByClientIdAsync(int clientId) =>
            Task.FromResult<IReadOnlyList<МеткаРасчетDto>>([]);

        public Task<МеткаРасчетDto> CreateAsync(
            int clientId, DateTime? date, string? comment) =>
            Task.FromResult(new МеткаРасчетDto());
    }

    private sealed class FakeVoiceSearch : IClientVoiceSearchService
    {
        public Task<IReadOnlyList<ClientCandidate>> SearchAsync(
            string text, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClientCandidate>>([]);
    }
}
