/* Вектор эмбеддинга для семантического поиска по номенклатуре.

   SQL Server 2022: типа VECTOR нет, CAST(binary AS real) запрещён
   («Explicit conversion from data type binary to real is not allowed»),
   поэтому T-SQL-функция косинуса на SUBSTRING/CAST не работает.
   Храним varbinary(3072) = 768 float * 4 байта, косинус считаем в C#
   (SkladApi, GET /api/nomenclature/search). */

IF COL_LENGTH('dbo.Номенклатура', 'Вектор') IS NULL
    ALTER TABLE dbo.[Номенклатура] ADD [Вектор] varbinary(3072) NULL;
GO
