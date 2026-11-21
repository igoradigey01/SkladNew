using Microsoft.AspNetCore.Mvc;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Repositories;
using Xunit;

namespace SkladApi.Tests;

public class AuditHistoryEndpointTests
{
    [Theory]
    [InlineData("invoice", "Invoice")]
    [InlineData("INVOICE", "Invoice")]
    [InlineData("payment", "Payment")]
    [InlineData("Платёж", "Payment")]
    public async Task Normalizes_entity_type(string requested, string expected)
    {
        var repository = new FakeAuditLogRepository();
        var controller = new AuditController(repository);

        var result = await controller.GetHistory(
            requested, 10, 50, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, repository.LastEntityType);
        Assert.Equal(10, repository.LastEntityId);
    }

    [Fact]
    public async Task Returns_empty_history_as_200()
    {
        var controller = new AuditController(new FakeAuditLogRepository());

        var result = await controller.GetHistory(
            "invoice", 5, 50, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<AuditLogDto>>(ok.Value);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Returns_history_in_repository_order()
    {
        var newer = Entry(id: 2, timestamp: new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc));
        var older = Entry(id: 1, timestamp: new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc));
        var repository = new FakeAuditLogRepository([newer, older]);
        var controller = new AuditController(repository);

        var result = await controller.GetHistory(
            "payment", 77, 50, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<AuditLogDto>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.True(items[0].Timestamp > items[1].Timestamp);
        Assert.Equal("Payment", items[0].EntityType);
    }

    [Fact]
    public async Task Rejects_unknown_entity_type()
    {
        var controller = new AuditController(new FakeAuditLogRepository());

        var result = await controller.GetHistory(
            "client", 1, 50, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Rejects_non_positive_entity_id(long entityId)
    {
        var controller = new AuditController(new FakeAuditLogRepository());

        var result = await controller.GetHistory(
            "invoice", entityId, 50, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task Rejects_limit_out_of_range(int limit)
    {
        var controller = new AuditController(new FakeAuditLogRepository());

        var result = await controller.GetHistory(
            "invoice", 1, limit, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Passes_limit_to_repository()
    {
        var repository = new FakeAuditLogRepository();
        var controller = new AuditController(repository);

        await controller.GetHistory("invoice", 1, 7, CancellationToken.None);

        Assert.Equal(7, repository.LastLimit);
    }

    private static AuditLogDto Entry(long id, DateTime timestamp) => new()
    {
        Id = id,
        EntityType = "Payment",
        EntityId = 77,
        Action = "Update",
        ChangedByEmployeeId = 1,
        ChangedByEmployeeName = "Сидорова Мария Ивановна",
        Timestamp = timestamp,
        OldData = """{"sum":1000}""",
        NewData = """{"sum":1200}""",
    };

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        private readonly IReadOnlyList<AuditLogDto> _items;

        public FakeAuditLogRepository(IReadOnlyList<AuditLogDto>? items = null) =>
            _items = items ?? [];

        public string? LastEntityType { get; private set; }
        public long LastEntityId { get; private set; }
        public int LastLimit { get; private set; }

        public Task<IReadOnlyList<AuditLogDto>> GetAsync(
            string entityType,
            long entityId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            LastEntityType = entityType;
            LastEntityId = entityId;
            LastLimit = limit;
            return Task.FromResult(_items);
        }
    }
}
