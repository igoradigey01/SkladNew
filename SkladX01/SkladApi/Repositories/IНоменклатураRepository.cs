using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IНоменклатураRepository
{
    Task<IReadOnlyList<НоменклатураDto>> GetActiveAsync();

    /// <summary>Добавляет колонку [Вектор], если её ещё нет.</summary>
    Task EnsureVectorColumnAsync();

    /// <summary>Активные позиции без сохранённого вектора.</summary>
    Task<IReadOnlyList<НоменклатураВекторDto>> GetWithoutVectorAsync();

    /// <summary>Активные позиции с сохранённым вектором.</summary>
    Task<IReadOnlyList<НоменклатураВекторDto>> GetWithVectorAsync();

    Task UpdateVectorAsync(int id, byte[] vector);
}
