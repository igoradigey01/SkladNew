using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using SkladDB.Data;
using SkladDB.Models;
using SkladDB.Services;
using Xunit;

namespace SkladApi.Tests;

public class SupplierDealsEndpointTests
{
    private static readonly Guid DealId = Guid.Parse("F0000000-0000-0000-0000-000001202310");
    private static readonly Guid ReceiptId = Guid.Parse("A0000000-0000-0000-0000-000000000127");

    [Fact]
    public async Task Returns_supplier_deals()
    {
        var deal = new СделкаПоставщикаDto
        {
            Id = DealId,
            Номер = "P-000001",
            Долг = 100,
            Комментарий = "Поставка 2023-10",
        };

        var (controller, _) = Create(deals: [deal]);

        var result = await controller.GetSupplierDeals(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var deals = Assert.IsAssignableFrom<IReadOnlyList<СделкаПоставщикаDto>>(ok.Value);
        Assert.Single(deals);
        Assert.Equal("P-000001", deals[0].Номер);
    }

    [Fact]
    public async Task Summary_returns_not_found_for_unknown_supplier()
    {
        var (controller, _) = Create(summary: null);

        var result = await controller.GetSupplierSummary(999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Summary_returns_totals()
    {
        var summary = new ПоставщикSummaryDto
        {
            ПоставщикId = 1,
            Название = "Смоленск",
            Сделок = 7,
            Получено = 1000,
            Отдано = 400,
            Долг = 600,
        };

        var (controller, _) = Create(summary: summary);

        var result = await controller.GetSupplierSummary(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<ПоставщикSummaryDto>(ok.Value);
        Assert.Equal(7, value.Сделок);
        Assert.Equal(600, value.Долг);
    }

    [Fact]
    public async Task Products_are_requested_for_the_deal()
    {
        var repository = new StubReceivedGoodsRepository
        {
            Items =
            [
                new ПолученныйТоварDto
                {
                    Id = ReceiptId,
                    Номер = "П-000127",
                    Дата = new DateTime(2024, 5, 28),
                    Сумма = 729600,
                    СделкаId = DealId,
                    AuditId = 127,
                    Изменялась = true,
                },
            ],
        };

        var (controller, _) = Create(products: repository);

        var result = await controller.GetDealProducts(DealId);

        Assert.Equal(DealId, repository.LastDealId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var products = Assert.IsAssignableFrom<IReadOnlyList<ПолученныйТоварDto>>(ok.Value);
        Assert.True(products[0].Изменялась);
    }

    [Fact]
    public async Task Payments_are_requested_for_the_deal()
    {
        var repository = new StubSupplierPaymentsRepository
        {
            Items =
            [
                new ОтданныеДеньгиDto
                {
                    Id = 5000,
                    Дата = new DateTime(2023, 11, 5),
                    Сумма = 4374860,
                    СделкаId = DealId,
                },
            ],
        };

        var (controller, _) = Create(payments: repository);

        var result = await controller.GetDealPayments(DealId);

        Assert.Equal(DealId, repository.LastDealId);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<ОтданныеДеньгиDto>>(ok.Value);
        Assert.Single(items);
    }

    [Fact]
    public async Task Receipt_rows_return_not_found_for_foreign_deal()
    {
        var (controller, _) = Create(
            products: new StubReceivedGoodsRepository { ReceiptBelongs = false });

        var result = await controller.GetReceiptRows(DealId, ReceiptId, default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Receipt_rows_are_returned_for_the_receipt()
    {
        var repository = new StubReceivedGoodsRepository
        {
            Rows =
            [
                new СтрокаПриходаDto
                {
                    Id = 127,
                    НоменклатураId = 5,
                    Номенклатура = "ДСП 16 мм",
                    Количество = 3,
                    Цена = 100,
                    Сумма = 300,
                },
            ],
        };

        var (controller, _) = Create(products: repository);

        var result = await controller.GetReceiptRows(DealId, ReceiptId, default);

        Assert.Equal(ReceiptId, repository.LastReceiptId);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<СтрокаПриходаDto>>(ok.Value);
        Assert.Single(rows);
        Assert.Equal("ДСП 16 мм", rows[0].Номенклатура);
    }

    [Fact]
    public async Task Receipt_update_changes_quantity_price_and_sum()
    {
        var rows = new List<СтрокаПриходаDto>
        {
            new()
            {
                Id = 127,
                НоменклатураId = 5,
                Номенклатура = "ДСП 16 мм",
                Количество = 3,
                Цена = 100,
                Сумма = 300,
            },
        };

        var repository = new StubReceivedGoodsRepository { Rows = rows };

        var (controller, context) = Create(
            products: repository,
            receiptRows: rows);

        var result = await controller.UpdateReceiptRows(
            DealId,
            ReceiptId,
            new ОбновитьПриходDto
            {
                Строки =
                [
                    new ОбновитьСтрокуПриходаDto
                    {
                        Id = 127,
                        Количество = 4,
                        Цена = 125,
                    },
                ],
            },
            default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsAssignableFrom<IReadOnlyList<СтрокаПриходаDto>>(ok.Value);
        Assert.Single(updated);

        var saved = await context.Приходы.SingleAsync(x => x.Id == 127);
        Assert.Equal(4, saved.Quantity);
        Assert.Equal(125, saved.Price);
        Assert.Equal(500, saved.Sum);
    }

    [Fact]
    public async Task Payment_update_changes_sum_only()
    {
        var payment = new ОтданныеДеньгиDto
        {
            Id = 5000,
            Дата = new DateTime(2023, 11, 5),
            Сумма = 1000,
            СделкаId = DealId,
        };

        var payments = new StubSupplierPaymentsRepository { Item = payment };

        var (controller, context) = Create(
            payments: payments,
            seededPayment: payment);

        var result = await controller.UpdatePayment(
            DealId,
            5000,
            new ОбновитьПлатежDto { Сумма = 750 },
            default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var updated = Assert.IsType<ОтданныеДеньгиDto>(ok.Value);
        Assert.Equal(750, updated.Сумма);

        var saved = await context.Деньги.SingleAsync(x => x.Id == 5000);
        Assert.Equal(750, saved.Sum);
    }

    private static (ПоставщикController Controller, SkladContext Context) Create(
        IReadOnlyList<СделкаПоставщикаDto>? deals = null,
        ПоставщикSummaryDto? summary = null,
        StubReceivedGoodsRepository? products = null,
        StubSupplierPaymentsRepository? payments = null,
        IReadOnlyList<СтрокаПриходаDto>? receiptRows = null,
        ОтданныеДеньгиDto? seededPayment = null)
    {
        var options = new DbContextOptionsBuilder<SkladContext>()
            .UseInMemoryDatabase($"supplier-{Guid.NewGuid()}")
            .Options;

        // InMemory не умеет обновлять «несуществующие» строки, поэтому строки
        // прихода и выплаты кладём отдельным контекстом — как в настоящей БД.
        if (receiptRows is not null || seededPayment is not null)
        {
            using var seed = new SkladContext(options, new FakeCurrentUserService());

            if (receiptRows is not null)
            {
                SeedReceipt(seed, receiptRows);
            }

            if (seededPayment is not null)
            {
                SeedPayment(seed, seededPayment);
            }
        }

        var context = new SkladContext(options, new FakeCurrentUserService());

        var controller = new ПоставщикController(
            new StubSupplierRepository(summary),
            new StubSupplierDealsRepository(deals ?? []),
            products ?? new StubReceivedGoodsRepository(),
            payments ?? new StubSupplierPaymentsRepository(),
            new NoopAuditService(),
            context);

        return (controller, context);
    }

    private static void SeedReceipt(
        SkladContext context,
        IReadOnlyList<СтрокаПриходаDto> rows)
    {
        foreach (var row in rows)
        {
            context.Приходы.Add(new Приход
            {
                Id = row.Id,
                UETR = ReceiptId,
                Date = new DateTime(2024, 5, 28),
                Type = 1,
                ПоставщикId = 1,
                НоменклатураId = row.НоменклатураId,
                СкладId = 1,
                Quantity = row.Количество,
                Price = row.Цена,
                Sum = row.Сумма,
            });
        }

        context.SaveChanges();
    }

    private static void SeedPayment(SkladContext context, ОтданныеДеньгиDto payment)
    {
        context.Деньги.Add(new Деньги
        {
            Id = payment.Id,
            UETR = Guid.NewGuid(),
            Date = payment.Дата,
            OperationType = 2,
            CounterpartyId = 1,
            CounterpartyType = 2,
            Sum = payment.Сумма,
            PaymentMethod = 1,
        });

        context.SaveChanges();
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

    private sealed class StubSupplierRepository(ПоставщикSummaryDto? summary)
        : IПоставщикRepository
    {
        public Task<IReadOnlyList<ПоставщикDto>> GetAllAsync(
            string? query, string sort, bool descending) =>
            Task.FromResult<IReadOnlyList<ПоставщикDto>>([]);

        public Task<ПоставщикDto?> GetByIdAsync(int id) =>
            Task.FromResult<ПоставщикDto?>(null);

        public Task<ПоставщикSummaryDto?> GetSummaryAsync(int id) =>
            Task.FromResult(summary);
    }

    private sealed class StubSupplierDealsRepository(
        IReadOnlyList<СделкаПоставщикаDto> deals) : IСделкаПоставщикаRepository
    {
        public Task<IReadOnlyList<СделкаПоставщикаDto>> GetBySupplierIdAsync(
            int supplierId) => Task.FromResult(deals);
    }

    private sealed class StubReceivedGoodsRepository : IПолученныйТоварRepository
    {
        public IReadOnlyList<ПолученныйТоварDto> Items { get; init; } = [];
        public IReadOnlyList<СтрокаПриходаDto> Rows { get; set; } = [];
        public bool ReceiptBelongs { get; init; } = true;
        public Guid? LastDealId { get; private set; }
        public Guid? LastReceiptId { get; private set; }

        public Task<IReadOnlyList<ПолученныйТоварDto>> GetByDealIdAsync(Guid dealId)
        {
            LastDealId = dealId;
            return Task.FromResult(Items);
        }

        public Task<bool> ReceiptBelongsToDealAsync(
            Guid dealId,
            Guid receiptId,
            CancellationToken cancellationToken = default)
        {
            LastDealId = dealId;
            LastReceiptId = receiptId;
            return Task.FromResult(ReceiptBelongs);
        }

        public Task<IReadOnlyList<СтрокаПриходаDto>> GetRowsAsync(
            Guid receiptId,
            CancellationToken cancellationToken = default)
        {
            LastReceiptId = receiptId;
            return Task.FromResult(Rows);
        }
    }

    private sealed class StubSupplierPaymentsRepository : IОтданныеДеньгиRepository
    {
        public IReadOnlyList<ОтданныеДеньгиDto> Items { get; init; } = [];
        public ОтданныеДеньгиDto? Item { get; init; }
        public Guid? LastDealId { get; private set; }

        public Task<IReadOnlyList<ОтданныеДеньгиDto>> GetByDealIdAsync(Guid dealId)
        {
            LastDealId = dealId;
            return Task.FromResult(Items);
        }

        public Task<ОтданныеДеньгиDto?> GetByIdAsync(
            Guid dealId,
            int paymentId,
            CancellationToken cancellationToken = default)
        {
            LastDealId = dealId;
            return Task.FromResult(Item);
        }
    }
}
