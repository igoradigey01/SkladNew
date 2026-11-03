using Microsoft.AspNetCore.Mvc;
using SkladApi.Dtos;
using SkladApi.Repositories;

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

    public КлиентController(
        IКлиентRepository клиентRepository,
        IMеткаРасчетRepository меткаРасчетRepository,
        IОтпущенныйТоварRepository отпущенныйТоварRepository,
        IПринятыеДеньгиRepository принятыеДеньгиRepository)
    {
        _клиентRepository = клиентRepository;
        _меткаРасчетRepository = меткаРасчетRepository;
        _отпущенныйТоварRepository = отпущенныйТоварRepository;
        _принятыеДеньгиRepository = принятыеДеньгиRepository;
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

    [HttpGet("marks/{markId:int}/products")]
    public async Task<ActionResult<IReadOnlyList<ОтпущенныйТоварDto>>> GetMarkProducts(int markId)
    {
        var products = await _отпущенныйТоварRepository.GetByMarkIdAsync(markId);

        return Ok(products);
    }

    [HttpGet("marks/{markId:int}/payments")]
    public async Task<ActionResult<IReadOnlyList<ПринятыеДеньгиDto>>> GetMarkPayments(int markId)
    {
        var payments = await _принятыеДеньгиRepository.GetByMarkIdAsync(markId);

        return Ok(payments);
    }
}
