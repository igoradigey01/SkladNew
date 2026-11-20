namespace SkladApi.Services;

/// <summary>
/// Определение формата изображения по сигнатуре.
/// Декодирование на сервере не нужно: модель получает файл как есть и сама приводит
/// картинку к своему размеру, а лишняя графическая библиотека тянет лицензию и CVE.
/// </summary>
public static class ImageSignature
{
    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> Gif87 => "GIF87a"u8;
    private static ReadOnlySpan<byte> Gif89 => "GIF89a"u8;
    private static ReadOnlySpan<byte> Bmp => "BM"u8;

    /// <summary>Проверяет, что поток — изображение поддерживаемого формата.</summary>
    public static bool TryIdentify(ReadOnlySpan<byte> header, out string? format, out string? error)
    {
        format = null;
        error = null;

        if (header.Length >= 3 && header[..3].SequenceEqual(Jpeg))
        {
            format = "jpeg";
            return true;
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(Png))
        {
            format = "png";
            return true;
        }

        if (header.Length >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WEBP"u8))
        {
            format = "webp";
            return true;
        }

        if (header.Length >= 6
            && (header[..6].SequenceEqual(Gif87) || header[..6].SequenceEqual(Gif89)))
        {
            format = "gif";
            return true;
        }

        if (header.Length >= 2 && header[..2].SequenceEqual(Bmp))
        {
            format = "bmp";
            return true;
        }

        if (IsHeif(header))
        {
            error =
                "HEIC/HEIF не поддерживается моделью. Сконвертируйте фото в JPEG на клиенте.";
            return false;
        }

        error = "Файл не является изображением (поддерживаются JPEG, PNG, WebP, GIF, BMP).";
        return false;
    }

    private static bool IsHeif(ReadOnlySpan<byte> header)
    {
        if (header.Length < 12 || !header[4..8].SequenceEqual("ftyp"u8))
        {
            return false;
        }

        ReadOnlySpan<byte> brand = header[8..12];

        return brand.SequenceEqual("heic"u8)
            || brand.SequenceEqual("heix"u8)
            || brand.SequenceEqual("hevc"u8)
            || brand.SequenceEqual("mif1"u8)
            || brand.SequenceEqual("msf1"u8)
            || brand.SequenceEqual("avif"u8);
    }
}
