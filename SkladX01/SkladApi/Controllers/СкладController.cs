using Microsoft.AspNetCore.Mvc;
using SkladApi.Dtos;
using SkladApi.Repositories;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/warehouses")]
public class СкладController : ControllerBase
{
    private readonly IСкладRepository _складRepository;

    public СкладController(IСкладRepository складRepository)
    {
        _складRepository = складRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<СкладDto>>> GetWarehouses()
    {
        var warehouses = await _складRepository.GetAllAsync();

        return Ok(warehouses);
    }
}
