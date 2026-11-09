using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly string _connectionString;

    public HealthController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SkladDb") ?? string.Empty;
    }

    /// <summary>Диагностика: версия SQL Server и доступность бинарных преобразований.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = new Dictionary<string, object?>();

        try
        {
            await using var connection = new SqlConnection(_connectionString);

            var version = await connection.ExecuteScalarAsync<string>("SELECT @@VERSION");
            result["db"] = version?.Split('\n')[0].Trim();

            result["binaryToReal"] = await TryScalarAsync(
                connection, "SELECT CAST(CAST(0x0000803F AS binary(4)) AS real)");

            result["vectorType"] = await TryScalarAsync(
                connection, "SELECT CAST('[1.0,2.0]' AS VECTOR(2))");
        }
        catch (Exception exception)
        {
            result["db"] = $"error: {exception.Message}";
        }

        return Ok(result);
    }

    private static async Task<object?> TryScalarAsync(SqlConnection connection, string sql)
    {
        try
        {
            return await connection.ExecuteScalarAsync<object>(sql);
        }
        catch (Exception exception)
        {
            return $"error: {exception.Message.Split('\n')[0]}";
        }
    }
}
