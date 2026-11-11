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

    public ПоставщикController(IПоставщикRepository поставщикRepository)
    {
        _поставщикRepository = поставщикRepository;
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
}
