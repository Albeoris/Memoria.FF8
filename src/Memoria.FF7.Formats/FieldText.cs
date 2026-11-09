using System.Text;

namespace Memoria.FF7.Formats;

public enum FieldTextTokenKind
{
    Glyph,
    ChoiceTab,
    Tab,
    NewLine,
    NewPage,
    CharacterName,
    PartyName,
    ControllerButton,
    Color,
    Flash,
    Rainbow,
    Pause,
    Wait,
    DecimalVariable,
    HexVariable,
    Scroll,
    RightAlignedVariable,
    MemoryString,
    FixedSpacing
}

public sealed record FieldTextToken(FieldTextTokenKind Kind, int Offset, byte Code, int Argument, int Argument2, string Text);

public static class FieldText
{
    private static readonly Encoding Roman = CodePagesEncodingProvider.Instance.GetEncoding(10000) ?? throw new InvalidOperationException("Mac Roman encoding is unavailable.");

    public static IReadOnlyList<FieldTextToken> Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        List<FieldTextToken> tokens = new();
        while (cursor.Remaining > 0)
        {
            int offset = cursor.Position;
            byte code = cursor.U8();
            if (code == 255)
            {
                break;
            }
            if (code < 0xE0)
            {
                tokens.Add(new FieldTextToken(FieldTextTokenKind.Glyph, offset, code, 0, 0, DecodeEnglishGlyph(code)));
                continue;
            }
            FieldTextToken token;
            if (code == 0xFE)
            {
                byte control = cursor.U8();
                FieldTextTokenKind kind = control switch
                {
                    >= 0xD2 and <= 0xD9 => FieldTextTokenKind.Color,
                    0xDA => FieldTextTokenKind.Flash,
                    0xDB => FieldTextTokenKind.Rainbow,
                    0xDC => FieldTextTokenKind.Pause,
                    0xDD => FieldTextTokenKind.Wait,
                    0xDE => FieldTextTokenKind.DecimalVariable,
                    0xDF => FieldTextTokenKind.HexVariable,
                    0xE0 => FieldTextTokenKind.Scroll,
                    0xE1 => FieldTextTokenKind.RightAlignedVariable,
                    0xE2 => FieldTextTokenKind.MemoryString,
                    0xE9 => FieldTextTokenKind.FixedSpacing,
                    _ => throw new InvalidDataException($"Unsupported western FF7 text control FE {control:X2} at offset {offset}.")
                };
                int argument = kind is FieldTextTokenKind.Wait or FieldTextTokenKind.MemoryString ? cursor.U16() : kind == FieldTextTokenKind.Color ? control - 0xD2 : 0;
                int argument2 = kind == FieldTextTokenKind.MemoryString ? cursor.U16() : 0;
                token = new FieldTextToken(kind, offset, control, argument, argument2, "");
            }
            else
            {
                FieldTextTokenKind kind = code switch
                {
                    0xE0 => FieldTextTokenKind.ChoiceTab,
                    0xE1 => FieldTextTokenKind.Tab,
                    0xE2 or 0xE3 or 0xE4 or 0xE6 => FieldTextTokenKind.Glyph,
                    0xE7 => FieldTextTokenKind.NewLine,
                    0xE8 => FieldTextTokenKind.NewPage,
                    >= 0xEA and <= 0xF2 => FieldTextTokenKind.CharacterName,
                    >= 0xF3 and <= 0xF5 => FieldTextTokenKind.PartyName,
                    >= 0xF6 and <= 0xF9 => FieldTextTokenKind.ControllerButton,
                    _ => throw new InvalidDataException($"Unsupported western FF7 text byte {code:X2} at offset {offset}.")
                };
                int argument = kind == FieldTextTokenKind.CharacterName ? code - 0xEA : kind == FieldTextTokenKind.PartyName ? code - 0xF3 : 0;
                string text = code switch
                {
                    0xE2 => ", ",
                    0xE3 => ".\"",
                    0xE4 => "…\"",
                    0xE6 => "⑬",
                    0xF6 => "〇",
                    0xF7 => "△",
                    0xF8 => "☐",
                    0xF9 => "✕",
                    _ => ""
                };
                token = new FieldTextToken(kind, offset, code, argument, 0, text);
            }
            tokens.Add(token);
        }
        return tokens.AsReadOnly();
    }

    public static int FindTerminatedLength(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        while (cursor.Remaining > 0)
        {
            byte code = cursor.U8();
            if (code == 0xFF)
            {
                return cursor.Position - 1;
            }
            if (code == 0xFE)
            {
                byte control = cursor.U8();
                if (control == 0xDD)
                {
                    cursor.Skip(2);
                }
                else if (control == 0xE2)
                {
                    cursor.Skip(4);
                }
            }
        }
        throw new InvalidDataException("FF7 field text has no terminator.");
    }

    public static string DecodeEnglishGlyph(byte code)
    {
        if (code >= 0xE0)
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }
        return code switch
        {
            < 0x5F => ((char)(code + 32)).ToString(),
            0x5F or 0xAA or 0xD0 => " ",
            0x80 => "⌘",
            0x84 => "Ù",
            0x85 => "Û",
            0x86 => "¶",
            _ => Roman.GetString([(byte)(code + 32)])
        };
    }
}
