using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Assets.Localization;

public sealed record FieldAuditDecodedRow(int Ordinal, int Offset, int Length, string? Text, string[] Tokens, string? Error);
public sealed record FieldAuditDecodedBank(string Decoder, string? CharacterTableSha256, FieldAuditDecodedRow[] Rows)
{
    public bool Complete => Rows.All(row => row.Error is null);
}

/// <summary>Uses one decoder per field, checking byte boundaries before the vendored decoder can substitute or overread.</summary>
public static partial class FieldAuditDecoder
{
    public static FieldAuditDecodedBank Decode(byte[] bytes, string language, string field)
    {
        if (language is not ("en" or "fr" or "de" or "es" or "it" or "ja"))
        {
            throw new ArgumentException("Unsupported source language.", nameof(language));
        }
        // Validate the complete offset table independently of any source-language decoding failure.
        int count = MsdFile.Read(bytes, Encoding.Latin1).Texts.Count;
        bool japanese = language == "ja";
        FF8Encoding encoding = japanese ? FF8Encoding.CreateJapanese(FF8TextLanguage.JP, new DefaultJapaneseFieldCharacters(), field) : NativeTextEncoding.Create(language);
        FieldCharacterSet? table = encoding.FieldCharacters;
        string? tableHash = table is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(table.Characters)));
        List<FieldAuditDecodedRow> rows = [];
        for (int index = 0; index < count; index++)
        {
            int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(index * 4));
            int end = index + 1 == count ? bytes.Length : BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan((index + 1) * 4));
            try
            {
                ReadOnlySpan<byte> row = bytes.AsSpan(start, end - start);
                ValidateCharacters(row, japanese, table);
                string text = encoding.GetString(row);
                if (text != encoding.GetString(row))
                {
                    throw new InvalidDataException("Source decoding is unstable.");
                }
                rows.Add(new(index, start, end - start, text, ControlToken().Matches(text).Select(match => match.Value).ToArray(), null));
            }
            catch (Exception exception) when (exception is FormatException or InvalidDataException or InvalidOperationException or IndexOutOfRangeException or ArgumentException)
            {
                rows.Add(new(index, start, end - start, null, [], exception.Message));
            }
        }
        return new($"FF8Encoding/{(japanese ? "Japanese" : "European")}/{typeof(FF8Encoding).Assembly.GetName().Version}", tableHash, rows.ToArray());
    }

    private static void ValidateCharacters(ReadOnlySpan<byte> bytes, bool japanese, FieldCharacterSet? table)
    {
        for (int offset = 0; offset < bytes.Length; offset++)
        {
            byte value = bytes[offset];
            bool parameter = value is 3 or 4 or 5 or 6 or 9 or 10 or 11 or 12 or 14;
            bool escape = japanese && value is >= 0x18 and <= 0x1C;
            if (!parameter && !escape)
            {
                continue;
            }
            if (++offset >= bytes.Length)
            {
                throw new InvalidDataException($"Truncated text control/character escape at byte {offset - 1}.");
            }
            if (japanese && value == 0x18)
            {
                throw new InvalidDataException("Reserved Japanese character escape 0x18.");
            }
            if (japanese && value == 0x1C)
            {
                int index = bytes[offset] - 0x20;
                if (table is null || index < 0 || index >= table.Count)
                {
                    throw new InvalidDataException($"Missing or invalid field-character table entry {index}.");
                }
            }
        }
    }

    [GeneratedRegex(@"\{[^{}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex ControlToken();
}
