using Microsoft.AspNetCore.Mvc;
using SkladApi.Dtos;
using SkladApi.Repositories;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/nomenclature")]
public class NomenclatureController : ControllerBase
{
    private readonly IНоменклатураRepository _номенклатура;

    public NomenclatureController(IНоменклатураRepository номенклатура)
    {
        _номенклатура = номенклатура;
    }

    /// <summary>Активная номенклатура; поиск по названию или артикулу.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<НоменклатураDto>>> Get(
        [FromQuery] string? q,
        [FromQuery] int limit = 50)
    {
        var products = await _номенклатура.GetActiveAsync();

        if (!string.IsNullOrWhiteSpace(q))
        {
            products = products
                .Where(product =>
                    product.Название.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (product.Артикул?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        return Ok(products.Take(Math.Clamp(limit, 1, 200)).ToList());
    }
}
