namespace SkladApi.Services;

/// <summary>
/// Промты и JSON-схема разбора накладной.
/// Источник текста — <c>_hubx01/prompts/парсер.md</c>, <c>Whisper.md</c>,
/// <c> JSON Schema.md</c>. При изменении спеки в хабе правьте и здесь.
/// </summary>
internal static class RecognitionPrompts
{
    /// <summary>Роль парсера: приоритет источников и правила сверки документа с голосом.</summary>
    public const string System = """"
        Ты — точный парсер первичных бухгалтерских и складских документов (УПД, ТОРГ-12, товарные накладные). Твоя задача — извлечь данные с фотографии накладной, сопоставить их с голосовым комментарием приемщика и вернуть результат строго в формате JSON по заданной схеме.

        ПРАВИЛА ИЗВЛЕЧЕНИЯ И СВЕРКИ:
        1. Приоритет источников:
           - Номер документа, дата, поставщик, наименования товаров, артикулы, единицы измерения, цены и суммы бери СТРОГО с фотографии накладной.
           - Фактическое принятое количество, выявленный брак, пересорт и комментарии бери из текста голосовой расшифровки.

        2. Логика расчета количества (qty_fact):
           - Если в голосовом комментарии не упоминается конкретная позиция или сказано «всё по накладной / всё приняли / без расхождений», то qty_fact равно qty_doc (количеству из документа).
           - Если в голосе указано другое количество (например: «молока пришло 8 вместо 10» или «минус две банки тушенки»), запиши в qty_fact фактическое принятое число (8), а расхождение зафиксируй в поле comment.
           - Если приемщик упоминает новый товар, которого нет на фото накладной, добавь его в список items с qty_doc = 0, заполни qty_fact из голоса и поставь флаг is_extra = true.

        3. Форматирование данных:
           - Числа (цены, количества, суммы) записывай строго как числа (float или int) с точкой, без пробелов и символов валюты (например: 1250.50).
           - Если какое-то текстовое поле на фото нечитаемо или отсутствует, ставь null. Не придумывай данные.
           - Не пиши никаких пояснений до или после JSON. Возвращай только валидный JSON-объект.
        """";

    /// <summary>Шаблон запроса: фото накладной + голос приёмщика. <c>{voice_transcript}</c> подставляется.</summary>
    public const string UserTemplate = """"
        Проанализируй прикрепленное фото накладной и учти комментарий приемщика.

        ГОЛОСОВОЙ КОММЕНТАРИЙ ПРИЕМЩИКА:
        """
        {voice_transcript}
        """

        Извлеки шапку документа и все товарные позиции в строгом соответствии со схемой JSON.
        """";

    /// <summary>Схема ответа из <c> JSON Schema.md</c> — она же уходит в <c>format</c> Ollama.</summary>
    public static object Schema { get; } = new
    {
        type = "object",
        properties = new
        {
            document_number = new { type = new[] { "string", "null" } },
            document_date = new { type = new[] { "string", "null" } },
            supplier_name = new { type = new[] { "string", "null" } },
            general_comment = new { type = new[] { "string", "null" } },
            items = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        line_number = new { type = new[] { "integer", "null" } },
                        article = new { type = new[] { "string", "null" } },
                        name = new { type = "string" },
                        unit = new { type = new[] { "string", "null" } },
                        price = new { type = new[] { "number", "null" } },
                        qty_doc = new { type = "number" },
                        qty_fact = new { type = "number" },
                        is_extra = new { type = "boolean" },
                        item_comment = new { type = new[] { "string", "null" } },
                    },
                    required = new[] { "name", "qty_doc", "qty_fact", "is_extra" },
                },
            },
        },
        required = new[] { "document_number", "document_date", "supplier_name", "items" },
    };

    /// <summary>Подставляет расшифровку речи в шаблон; пустая расшифровка — «без расхождений».</summary>
    public static string BuildUserPrompt(string? transcription)
    {
        var voice = string.IsNullOrWhiteSpace(transcription)
            ? "Приёмщик комментария не оставил — всё принято по накладной, без расхождений."
            : transcription.Trim();

        return UserTemplate.Replace("{voice_transcript}", voice, StringComparison.Ordinal);
    }
}
