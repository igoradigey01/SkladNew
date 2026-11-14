using Microsoft.AspNetCore.Mvc;
using SkladApi.Dtos;
using SkladApi.Repositories;

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

    public ПоставщикController(
        IПоставщикRepository поставщикRepository,
        IСделкаПоставщикаRepository сделкаПоставщикаRepository,
        IПолученныйТоварRepository полученныйТоварRepository,
        IОтданныеДеньгиRepository отданныеДеньгиRepository)
    {
        _поставщикRepository = поставщикRepository;
        _сделкаПоставщикаRepository = сделкаПоставщикаRepository;
        _полученныйТоварRepository = полученныйТоварRepository;
        _отданныеДеньгиRepository = отданныеДеньгиRepository;
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
}
