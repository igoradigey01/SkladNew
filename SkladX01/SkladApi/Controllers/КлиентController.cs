using Microsoft.AspNetCore.Mvc;
using SkladApi.Dtos;
using SkladApi.Repositories;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/clients")]
public class КлиентController : ControllerBase
{
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
        [FromQuery] string? city = null)
    {
        var clients = await _клиентRepository.GetAllAsync(phone, alias, city);

        return Ok(clients);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<КлиентDto>> GetClient(int id)
    {
        var client = await _клиентRepository.GetByIdAsync(id);

        if (client is null)
        {
            return NotFound();
        }

        return Ok(client);
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
