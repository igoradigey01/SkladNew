using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;
using SkladDB.Data;
using SkladDB.Models;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/clients")]
public class КлиентController : ControllerBase
{
    private static readonly string[] AllowedSort = ["id", "alias", "name", "phone", "city"];
    private static readonly string[] AllowedDir = ["asc", "desc"];

    private readonly IКлиентRepository _клиентRepository;
    private readonly IMеткаРасчетRepository _меткаРасчетRepository;
    private readonly IОтпущенныйТоварRepository _отпущенныйТоварRepository;
    private readonly IПринятыеДеньгиRepository _принятыеДеньгиRepository;
    private readonly IClientVoiceSearchService _voiceSearch;
    private readonly IAuditService _auditService;
    private readonly SkladContext _context;

    public КлиентController(
        IКлиентRepository клиентRepository,
        IMеткаРасчетRepository меткаРасчетRepository,
        IОтпущенныйТоварRepository отпущенныйТоварRepository,
        IПринятыеДеньгиRepository принятыеДеньгиRepository,
        IClientVoiceSearchService voiceSearch,
        IAuditService auditService,
        SkladContext context)
    {
        _клиентRepository = клиентRepository;
        _меткаРасчетRepository = меткаРасчетRepository;
        _отпущенныйТоварRepository = отпущенныйТоварRepository;
        _принятыеДеньгиRepository = принятыеДеньгиRepository;
        _voiceSearch = voiceSearch;
        _auditService = auditService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<КлиентDto>>> GetClients(
        [FromQuery] string? phone = null,
        [FromQuery] string? alias = null,
        [FromQuery] string? city = null,
        [FromQuery] string? q = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? dir = null,
        [FromQuery] bool activeOnly = false)
    {
        var sortKey = (sort ?? "alias").Trim().ToLowerInvariant();
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

        var clients = await _клиентRepository.GetAllAsync(
            phone, alias, city, q, sortKey, dirKey == "desc", activeOnly);

        return Ok(clients);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<КлиентDto>> GetClient(int id)
    {
        var client = await _клиентRepository.GetByIdAsync(id);

        return client is null ? NotFound() : Ok(client);
    }

    [HttpGet("{id:int}/summary")]
    public async Task<ActionResult<КлиентSummaryDto>> GetClientSummary(int id)
    {
        var summary = await _клиентRepository.GetSummaryAsync(id);

        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:int}/marks")]
    public async Task<ActionResult<IReadOnlyList<МеткаРасчетDto>>> GetClientMarks(int id)
    {
        var marks = await _меткаРасчетRepository.GetByClientIdAsync(id);

        return Ok(marks);
    }

    [HttpGet("marks/{dealId:guid}/products")]
    public async Task<ActionResult<IReadOnlyList<ОтпущенныйТоварDto>>> GetDealProducts(Guid dealId)
    {
        var products = await _отпущенныйТоварRepository.GetByDealIdAsync(dealId);

        return Ok(products);
    }

    [HttpGet("marks/{dealId:guid}/payments")]
    public async Task<ActionResult<IReadOnlyList<ПринятыеДеньгиDto>>> GetDealPayments(Guid dealId)
    {
        var payments = await _принятыеДеньгиRepository.GetByDealIdAsync(dealId);

        return Ok(payments);
    }

    /// <summary>Строки накладной для окна редактирования.</summary>
    [HttpGet("marks/{dealId:guid}/products/{id:int}/rows")]
    public async Task<ActionResult<IReadOnlyList<СтрокаНакладнойDto>>> GetInvoiceRows(
        Guid dealId,
        int id,
        CancellationToken cancellationToken)
    {
        var belongs = await _отпущенныйТоварRepository.InvoiceBelongsToDealAsync(
            dealId, id, cancellationToken);

        if (!belongs)
        {
            return NotFound();
        }

        var rows = await _отпущенныйТоварRepository.GetRowsAsync(id, cancellationToken);

        return Ok(rows);
    }

    /// <summary>
    /// Меняет количество и цену строк накладной. Сумма пересчитывается,
    /// добавление и удаление строк не поддерживается.
    /// </summary>
    [HttpPut("marks/{dealId:guid}/products/{id:int}")]
    public async Task<ActionResult<IReadOnlyList<СтрокаНакладнойDto>>> UpdateInvoiceRows(
        Guid dealId,
        int id,
        [FromBody] ОбновитьНакладнуюDto? dto,
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

        var belongs = await _отпущенныйТоварRepository.InvoiceBelongsToDealAsync(
            dealId, id, cancellationToken);

        if (!belongs)
        {
            return NotFound();
        }

        var oldRows = await _отпущенныйТоварRepository.GetRowsAsync(id, cancellationToken);
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
                var entity = new Расход { Id = change.Id };
                _context.Расходы.Attach(entity);
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
                AuditEntityTypes.Invoice,
                id,
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
                return Conflict(new { error = "Не удалось сохранить строки накладной." });
            }
        }

        var updated = await _отпущенныйТоварRepository.GetRowsAsync(id, cancellationToken);

        return Ok(updated);
    }

    /// <summary>Меняет только сумму платежа; остальные реквизиты неизменны.</summary>
    [HttpPut("marks/{dealId:guid}/payments/{id:int}")]
    public async Task<ActionResult<ПринятыеДеньгиDto>> UpdatePayment(
        Guid dealId,
        int id,
        [FromBody] ОбновитьПлатежDto? dto,
        CancellationToken cancellationToken)
    {
        if (dto is null)
        {
            return BadRequest(new { error = "Нужна новая сумма платежа." });
        }

        if (dto.Сумма < 0)
        {
            return BadRequest(new { error = "Сумма не может быть отрицательной." });
        }

        var old = await _принятыеДеньгиRepository.GetByIdAsync(
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
            AuditEntityTypes.Payment,
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
            return Conflict(new { error = "Не удалось сохранить сумму платежа." });
        }

        return Ok(new ПринятыеДеньгиDto
        {
            Id = old.Id,
            Дата = old.Дата,
            Сумма = dto.Сумма,
            СделкаId = old.СделкаId,
        });
    }

    [HttpPost("{id:int}/marks")]
    public async Task<ActionResult<МеткаРасчетDto>> CreateDeal(
        int id,
        [FromBody] CreateDealRequest? request)
    {
        var client = await _клиентRepository.GetByIdAsync(id);
        if (client is null)
        {
            return NotFound(new { error = "client not found", clientId = id });
        }

        var deal = await _меткаРасчетRepository.CreateAsync(
            id, request?.Дата, request?.Комментарий);

        return Created($"/api/clients/{id}/marks/{deal.Id}", deal);
    }

    /// <summary>Поиск клиента по продиктованному тексту.</summary>
    [HttpPost("voice-search")]
    public async Task<ActionResult<IReadOnlyList<ClientCandidate>>> SearchClientsByVoice(
        [FromBody] ClientVoiceQuery? query,
        CancellationToken cancellationToken)
    {
        var text = query?.RawText?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new { error = "Голосовой текст пуст." });
        }

        if (text.Length > ClientVoiceSearchService.MaxTextLength)
        {
            return BadRequest(new
            {
                error = $"Текст длиннее {ClientVoiceSearchService.MaxTextLength} символов.",
            });
        }

        var candidates = await _voiceSearch.SearchAsync(text, cancellationToken);

        return Ok(candidates);
    }
}
