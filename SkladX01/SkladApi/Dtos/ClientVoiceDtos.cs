namespace SkladApi.Dtos;

/// <summary>Текст, распознанный из голоса для поиска клиента.</summary>
public class ClientVoiceQuery
{
    public string RawText { get; set; } = string.Empty;
}

/// <summary>Кандидат клиента, отсортированный по релевантности.</summary>
public class ClientCandidate
{
    public int Id { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int Score { get; set; }
    public string MatchedField { get; set; } = string.Empty;
}

/// <summary>Что удалось выделить из фразы: имя, город, телефон.</summary>
public class ExtractedClientQuery
{
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
