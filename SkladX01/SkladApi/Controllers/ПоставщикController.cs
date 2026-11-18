using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using SkladDB.Data;
using SkladDB.Models;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/suppliers")]
public class ПоставщикController : ControllerBase
{
    private static readonly string[] AllowedSort = ["id", "name", "city"];
    private static readonly string[] AllowedDir = ["asc", "desc"];

    private readonly IПоставщикRepository _поставщикRepository;
    private readonly IСделкаПоставщикаRepository _сделкаПоставщикаRepository;
    private readonly IПолученныйТоварRepository _полученныйТоварRepository;
    private readonly IОтданныеДеньгиRepository _отданныеДеньгиRepository;
    private readonly IAuditService _auditService;
    private readonly SkladContext _context;

    public ПоставщикController(
        IПоставщикRepository поставщикRepository,
        IСделкаПоставщикаRepository сделкаПоставщикаRepository,
        IПолученныйТоварRepository полученныйТоварRepository,
        IОтданныеДеньгиRepository отданныеДеньгиRepository,
        IAuditService auditService,
        SkladContext context)
    {
        _поставщикRepository = поставщикRepository;
        _сделкаПоставщикаRepository = сделкаПоставщикаRepository;
        _полученныйТоварRepository = полученныйТоварRepository;
        _отданныеДеньгиRepository = отданныеДеньгиRepository;
        _auditService = auditService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ПоставщикDto>>> GetSuppliers(
        [FromQuery] string? q = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? dir = null)
    {
        var sortKey = (sort ?? "name").Trim().ToLowerInvariant();
        if (!AllowedSort.Contains(sortKey))
        {
            return BadRequest(new
            {
                error = "unknown sort",
                allowed = AllowedSort,
            });
        }

        var dirKey = (dir ?? "asc").Trim().ToLowerInvariant();
        if (!AllowedDir.Contains(dirKey))
        {
            return BadRequest(new
            {
                error = "unknown dir",
                allowed = AllowedDir,
            });
        }

        var suppliers = await _поставщикRepository.GetAllAsync(
            q, sortKey, dirKey == "desc");

        return Ok(suppliers);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ПоставщикDto>> GetSupplier(int id)
    {
        var supplier = await _поставщикRepository.GetByIdAsync(id);

        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<ПоставщикSummaryDto>> GetSupplierSummary(int id)
    {
        var summary = await _поставщикRepository.GetSummaryAsync(id);

        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:int}/deals")]
    public async Task<ActionResult<IReadOnlyList<СделкаПоставщикаDto>>> GetSupplierDeals(int id)
    {
        var deals = await _сделкаПоставщикаRepository.GetBySupplierIdAsync(id);

        return Ok(deals);
    }

    [HttpGet("deals/{dealId:guid}/products")]
    public async Task<ActionResult<IReadOnlyList<ПолученныйТоварDto>>> GetDealProducts(Guid dealId)
    {
        var products = await _полученныйТоварRepository.GetByDealIdAsync(dealId);

        return Ok(products);
    }

    [HttpGet("deals/{dealId:guid}/payments")]
    public async Task<ActionResult<IReadOnlyList<ОтданныеДеньгиDto>>> GetDealPayments(Guid dealId)
    {
        var payments = await _отданныеДеньгиRepository.GetByDealIdAsync(dealId);

        return Ok(payments);
    }

    /// <summary>Строки приходной накладной для правки.</summary>
    [HttpGet("deals/{dealId:guid}/receipts/{receiptId:guid}/rows")]
    public async Task<ActionResult<IReadOnlyList<СтрокаПриходаDto>>> GetReceiptRows(
        Guid dealId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        var belongs = await _полученныйТоварRepository.ReceiptBelongsToDealAsync(
            dealId, receiptId, cancellationToken);

        if (!belongs)
        {
            return NotFound();
        }

        var rows = await _полученныйТоварRepository.GetRowsAsync(
            receiptId, cancellationToken);

        return Ok(rows);
    }

    /// <summary>Меняет количество и цену строк приходной накладной.</summary>
    [HttpPut("deals/{dealId:guid}/receipts/{receiptId:guid}")]
    public async Task<ActionResult<IReadOnlyList<СтрокаПриходаDto>>> UpdateReceiptRows(
        Guid dealId,
        Guid receiptId,
        [FromBody] ОбновитьПриходDto? dto,
        CancellationToken cancellationToken)
    {
        if (dto?.Строки is not { Count: > 0 })
        {
            return BadRequest(new { error = "Нужна хотя бы одна строка." });
        }

        if (dto.Строки.Any(row => row.Количество <= 0))
        {
            return BadRequest(new { error = "Количество должно быть больше нуля." });
        }

        if (dto.Строки.Any(row => row.Цена < 0))
        {
            return BadRequest(new { error = "Цена не может быть отрицательной." });
        }

        if (dto.Строки.Select(row => row.Id).Distinct().Count() != dto.Строки.Count)
        {
            return BadRequest(new { error = "Строки не должны повторяться." });
        }

        var belongs = await _полученныйТоварRepository.ReceiptBelongsToDealAsync(
            dealId, receiptId, cancellationToken);

        if (!belongs)
        {
            return NotFound();
        }

        var oldRows = await _полученныйТоварRepository.GetRowsAsync(
            receiptId, cancellationToken);
        var oldById = oldRows.ToDictionary(row => row.Id);

        var changes = new List<(int Id, decimal Quantity, decimal Price, decimal Sum)>();

        foreach (var patch in dto.Строки)
        {
            if (!oldById.TryGetValue(patch.Id, out var old))
            {
                return NotFound();
            }

            var sum = Math.Round(
                patch.Количество * patch.Цена, 2, MidpointRounding.AwayFromZero);

            if (old.Количество == patch.Количество
                && old.Цена == patch.Цена
                && old.Сумма == sum)
            {
                continue;
            }

            changes.Add((patch.Id, patch.Количество, patch.Цена, sum));
        }

        if (changes.Count > 0)
        {
            foreach (var change in changes)
            {
                var entity = new Приход { Id = change.Id };
                _context.Приходы.Attach(entity);
                entity.Quantity = change.Quantity;
                entity.Price = change.Price;
                entity.Sum = change.Sum;

                var entry = _context.Entry(entity);
                entry.Property(e => e.Quantity).IsModified = true;
                entry.Property(e => e.Price).IsModified = true;
                entry.Property(e => e.Sum).IsModified = true;
            }

            var changeById = changes.ToDictionary(change => change.Id);

            var oldSnapshot = oldRows.Select(row => new
            {
                id = row.Id,
                nomenclatureId = row.НоменклатураId,
                quantity = row.Количество,
                price = row.Цена,
                sum = row.Сумма,
            }).ToList();

            var newSnapshot = oldRows.Select(row =>
            {
                if (changeById.TryGetValue(row.Id, out var change))
                {
                    return new
                    {
                        id = row.Id,
                        nomenclatureId = row.НоменклатураId,
                        quantity = change.Quantity,
                        price = change.Price,
                        sum = change.Sum,
                    };
                }

                return new
                {
                    id = row.Id,
                    nomenclatureId = row.НоменклатураId,
                    quantity = row.Количество,
                    price = row.Цена,
                    sum = row.Сумма,
                };
            }).ToList();

            await _auditService.LogChangeAsync(
                AuditEntityTypes.SupplierInvoice,
                oldRows.Min(row => row.Id),
                AuditActions.Update,
                oldSnapshot,
                newSnapshot,
                cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Conflict(new { error = "Не удалось сохранить строки прихода." });
            }
        }

        var updated = await _полученныйТоварRepository.GetRowsAsync(
            receiptId, cancellationToken);

        return Ok(updated);
    }

    /// <summary>Меняет только сумму выплаты поставщику.</summary>
    [HttpPut("deals/{dealId:guid}/payments/{id:int}")]
    public async Task<ActionResult<ОтданныеДеньгиDto>> UpdatePayment(
        Guid dealId,
        int id,
        [FromBody] ОбновитьПлатежDto? dto,
        CancellationToken cancellationToken)
    {
        if (dto is null)
        {
            return BadRequest(new { error = "Нужна новая сумма выплаты." });
        }

        if (dto.Сумма < 0)
        {
            return BadRequest(new { error = "Сумма не может быть отрицательной." });
        }

        var old = await _отданныеДеньгиRepository.GetByIdAsync(
            dealId, id, cancellationToken);

        if (old is null)
        {
            return NotFound();
        }

        if (old.Сумма == dto.Сумма)
        {
            return Ok(old);
        }

        var entity = new Деньги { Id = id };
        _context.Деньги.Attach(entity);
        entity.Sum = dto.Сумма;
        _context.Entry(entity).Property(e => e.Sum).IsModified = true;

        await _auditService.LogChangeAsync(
            AuditEntityTypes.SupplierPayment,
            id,
            AuditActions.Update,
            new { sum = old.Сумма },
            new { sum = dto.Сумма },
            cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "Не удалось сохранить сумму выплаты." });
        }

        return Ok(new ОтданныеДеньгиDto
        {
            Id = old.Id,
            Дата = old.Дата,
            Сумма = dto.Сумма,
            СделкаId = old.СделкаId,
        });
    }
}
