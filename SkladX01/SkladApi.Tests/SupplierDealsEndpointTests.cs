using Microsoft.AspNetCore.Mvc;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using Xunit;

namespace SkladApi.Tests;

public class SupplierDealsEndpointTests
{
    private static readonly Guid DealId = Guid.Parse("F0000000-0000-0000-0000-000001202310");

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

        var controller = Create(deals: [deal]);

        var result = await controller.GetSupplierDeals(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var deals = Assert.IsAssignableFrom<IReadOnlyList<СделкаПоставщикаDto>>(ok.Value);
        Assert.Single(deals);
        Assert.Equal("P-000001", deals[0].Номер);
    }

    [Fact]
    public async Task Summary_returns_not_found_for_unknown_supplier()
    {
        var controller = Create(summary: null);

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

        var controller = Create(summary: summary);

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
                    Id = Guid.Parse("A0000000-0000-0000-0000-000000000127"),
                    Номер = "П-000127",
                    Дата = new DateTime(2024, 5, 28),
                    Сумма = 729600,
                    СделкаId = DealId,
                },
            ],
        };

        var controller = Create(products: repository);

        await controller.GetDealProducts(DealId);

        Assert.Equal(DealId, repository.LastDealId);
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

        var controller = Create(payments: repository);

        var result = await controller.GetDealPayments(DealId);

        Assert.Equal(DealId, repository.LastDealId);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<ОтданныеДеньгиDto>>(ok.Value);
        Assert.Single(items);
    }

    private static ПоставщикController Create(
        IReadOnlyList<СделкаПоставщикаDto>? deals = null,
        ПоставщикSummaryDto? summary = null,
        StubReceivedGoodsRepository? products = null,
        StubSupplierPaymentsRepository? payments = null)
    {
        return new ПоставщикController(
            new StubSupplierRepository(summary),
            new StubSupplierDealsRepository(deals ?? []),
            products ?? new StubReceivedGoodsRepository(),
            payments ?? new StubSupplierPaymentsRepository());
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
        public Guid? LastDealId { get; private set; }

        public Task<IReadOnlyList<ПолученныйТоварDto>> GetByDealIdAsync(Guid dealId)
        {
            LastDealId = dealId;
            return Task.FromResult(Items);
        }
    }

    private sealed class StubSupplierPaymentsRepository : IОтданныеДеньгиRepository
    {
        public IReadOnlyList<ОтданныеДеньгиDto> Items { get; init; } = [];
        public Guid? LastDealId { get; private set; }

        public Task<IReadOnlyList<ОтданныеДеньгиDto>> GetByDealIdAsync(Guid dealId)
        {
            LastDealId = dealId;
            return Task.FromResult(Items);
        }
    }
}
