using System.Buffers.Binary;
using System.Text;

namespace SkladApi.Services;

/// <summary>
/// Проверка формата WAV до передачи в Whisper.net: движок ждёт PCM 16 кГц моно 16 бит,
/// а браузерный MediaRecorder отдаёт webm/opus — такой поток нужно конвертировать на клиенте.
/// </summary>
public static class WavFormat
{
    public const int RequiredSampleRate = 16000;
    public const short RequiredBitsPerSample = 16;
    public const short PcmCode = 1;

    private static ReadOnlySpan<byte> Riff => "RIFF"u8;
    private static ReadOnlySpan<byte> Wave => "WAVE"u8;
    private static ReadOnlySpan<byte> Fmt => "fmt "u8;

    /// <summary>Проверяет заголовок WAV. Возвращает false и текст причины.</summary>
    public static bool TryValidate(ReadOnlySpan<byte> bytes, out string? error)
    {
        error = null;

        if (bytes.Length < 12 || !bytes[..4].SequenceEqual(Riff) || !bytes[8..12].SequenceEqual(Wave))
        {
            error =
                "Файл не является WAV. Требуется WAV 16 кГц, моно, 16 бит " +
                "(браузерный webm/opus нужно сконвертировать на клиенте).";
            return false;
        }

        var position = 12;

        while (position + 8 <= bytes.Length)
        {
            var chunkId = bytes.Slice(position, 4);
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(position + 4, 4));

            if (chunkSize < 0)
            {
                break;
            }

            if (chunkId.SequenceEqual(Fmt))
            {
                if (position + 8 + 16 > bytes.Length)
                {
                    error = "Блок fmt в WAV обрезан.";
                    return false;
                }

                var format = bytes.Slice(position + 8, 16);
                var audioFormat = BinaryPrimitives.ReadInt16LittleEndian(format);
                var channels = BinaryPrimitives.ReadInt16LittleEndian(format[2..]);
                var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(format[4..]);
                var bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(format[14..]);

                if (audioFormat != PcmCode)
                {
                    error = $"Ожидается WAV PCM (format = 1), получено format = {audioFormat}. ";
                }
                else if (channels != 1)
                {
                    error = $"Ожидается моно, получено каналов: {channels}. ";
                }
                else if (sampleRate != RequiredSampleRate)
                {
                    error = $"Ожидается частота {RequiredSampleRate} Гц, получено {sampleRate} Гц. ";
                }
                else if (bitsPerSample != RequiredBitsPerSample)
                {
                    error = $"Ожидается разрядность {RequiredBitsPerSample} бит, получено {bitsPerSample}. ";
                }

                if (error is not null)
                {
                    error += "Сконвертируйте запись в WAV 16 кГц, моно, 16 бит на клиенте.";
                    return false;
                }

                return true;
            }

            // Чанки выровнены по чётной границе.
            position += 8 + chunkSize + (chunkSize & 1);
        }

        error = "В WAV нет блока fmt.";
        return false;
    }

    /// <summary>Собирает WAV 16 кГц, моно, 16 бит из сырых PCM-сэмплов (для тестов).</summary>
    public static byte[] BuildSilence(int samples)
    {
        const int bytesPerSample = 2;
        var dataSize = samples * bytesPerSample;

        using var stream = new MemoryStream(44 + dataSize);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Riff);
        writer.Write(36 + dataSize);
        writer.Write(Wave);
        writer.Write(Fmt);
        writer.Write(16);
        writer.Write(PcmCode);
        writer.Write((short)1);
        writer.Write(RequiredSampleRate);
        writer.Write(RequiredSampleRate * bytesPerSample);
        writer.Write((short)bytesPerSample);
        writer.Write(RequiredBitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);
        writer.Flush();

        return stream.ToArray();
    }
}
