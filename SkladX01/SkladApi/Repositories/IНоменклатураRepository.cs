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

    /// <summary>Быстрый поиск по Код/Артикул/Название для ввода в документ.</summary>
    Task<IReadOnlyList<НоменклатураQuickDto>> QuickSearchAsync(string query, int limit);

    /// <summary>Следующий свободный код вида НМ-00042 (по максимальному существующему).</summary>
    Task<string> GetNextCodeAsync();

    /// <summary>Справочники для быстрого создания: группы, виды, категории, единицы.</summary>
    Task<НоменклатураReferencesDto> GetReferencesAsync();

    /// <summary>Характеристики вида номенклатуры со списками допустимых значений.</summary>
    Task<IReadOnlyList<ХарактеристикаВидаDto>> GetCharacteristicsAsync(int видId);

    /// <summary>Создаёт позицию и значения характеристик в одной транзакции.</summary>
    Task<int> CreateAsync(СоздатьНоменклатуруRequest request);
}
