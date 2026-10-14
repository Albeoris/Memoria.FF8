using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeFadeWords([property: JsonRequired] short Mode, [property: JsonRequired] short Counter, [property: JsonRequired] short Rate, [property: JsonRequired] short Red, [property: JsonRequired] short Green, [property: JsonRequired] short Blue, [property: JsonRequired] short StartRed, [property: JsonRequired] short StartGreen, [property: JsonRequired] short StartBlue, [property: JsonRequired] short TargetRed, [property: JsonRequired] short TargetGreen, [property: JsonRequired] short TargetBlue)
{
    public static NativeFadeWords Zero() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public short At(int index) => index switch
    {
        0 => Mode,
        1 => Counter,
        2 => Rate,
        3 => Red,
        4 => Green,
        5 => Blue,
        6 => StartRed,
        7 => StartGreen,
        8 => StartBlue,
        9 => TargetRed,
        10 => TargetGreen,
        11 => TargetBlue,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public NativeFadeWords With(int index, short value) => index switch
    {
        0 => this with { Mode = value },
        1 => this with { Counter = value },
        2 => this with { Rate = value },
        3 => this with { Red = value },
        4 => this with { Green = value },
        5 => this with { Blue = value },
        6 => this with { StartRed = value },
        7 => this with { StartGreen = value },
        8 => this with { StartBlue = value },
        9 => this with { TargetRed = value },
        10 => this with { TargetGreen = value },
        11 => this with { TargetBlue = value },
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

public enum NativeFadeDrawMode
{
    None,
    Add,
    Subtract,
    Black
}
public sealed record NativeFadeDraw([property: JsonRequired] NativeFadeDrawMode Mode, [property: JsonRequired] byte Red, [property: JsonRequired] byte Green, [property: JsonRequired] byte Blue);

/// <summary>Physical1CE4868..487E and saved event216..238 are distinct native banks, owned here together.</summary>
public sealed record NativeFieldFadeState
{
    [JsonRequired]
    public required NativeFadeWords Physical { get; init; }
    [JsonRequired]
    public required NativeFadeWords Saved { get; init; }
    [JsonRequired]
    public required byte SyncMode { get; init; }
    [JsonRequired]
    public required NativeFadeDraw Draw { get; init; }
    [JsonRequired]
    public required byte MapFadeDisabled { get; init; }

    public static NativeFieldFadeState FromColdProcess() => new() { Physical = NativeFadeWords.Zero(), Saved = NativeFadeWords.Zero(), SyncMode = 0, Draw = new(NativeFadeDrawMode.None, 0, 0, 0), MapFadeDisabled = 0 };

    public void Validate()
    {
        if (Physical is null || Saved is null || Draw is null || !Enum.IsDefined(Draw.Mode))
        {
            throw new InvalidDataException("Native field fade requires both complete WORD banks and its executed drawing sample.");
        }
    }

    public NativeFieldFadeState ClearSavedForNewGame() => this with { Saved = NativeFadeWords.Zero() };
    public NativeFieldFadeState ResetPhysicalForLoader() => this with { Physical = Physical with { Mode = 0 }, Draw = new(NativeFadeDrawMode.None, 0, 0, 0) };
    public NativeFieldFadeState InitializeFreshActors() => this with { Physical = Physical with { Mode = 4 }, Saved = Saved with { Mode = 0 } };
    public NativeFieldFadeState SetMapFadeDisabled(bool disabled) => this with { MapFadeDisabled = disabled ? (byte)1 : (byte)0 };

    public NativeFieldFadeState IssueFadeIn() => IssueStandard(2, 255, 16);
    public NativeFieldFadeState IssueFadeOut() => IssueStandard(3, 0, 8);

    private NativeFieldFadeState IssueStandard(byte mode, short counter, short rate)
    {
        Validate();
        NativeFadeWords words = Physical with { Mode = mode, Counter = counter, Rate = rate, Red = 255, Green = 255, Blue = 255 };
        return this with { Physical = words, Saved = words, SyncMode = mode };
    }

    public NativeFieldFadeState IssueNone()
    {
        Validate();
        return this with { Physical = Physical with { Mode = 0 }, Saved = Saved with { Mode = 0 } };
    }

    public NativeFieldFadeState IssueBlack()
    {
        Validate();
        return this with { Physical = Physical with { Mode = 4 }, Saved = Saved with { Mode = 4 } };
    }

    public NativeFieldFadeState IssueDirectColor(bool subtract, int red, int green, int blue)
    {
        Validate();
        NativeFadeWords words = Physical with { Mode = (short)(subtract ? 8 : 7), Counter = 0, Red = unchecked((short)red), Green = unchecked((short)green), Blue = unchecked((short)blue) };
        return this with { Physical = words, Saved = words };
    }

    public NativeFieldFadeState IssueTargetColor(bool subtract, int red, int green, int blue, int duration)
    {
        Validate();
        return IssueInterpolatedColor(subtract, Physical.Red, Physical.Green, Physical.Blue, red, green, blue, duration);
    }

    public NativeFieldFadeState IssueInterpolatedColor(bool subtract, int startRed, int startGreen, int startBlue, int targetRed, int targetGreen, int targetBlue, int duration)
    {
        Validate();
        NativeFadeWords words = Physical with { Mode = (short)(subtract ? 6 : 5), Counter = 0, Rate = unchecked((short)duration), StartRed = unchecked((short)startRed), StartGreen = unchecked((short)startGreen), StartBlue = unchecked((short)startBlue), TargetRed = unchecked((short)targetRed), TargetGreen = unchecked((short)targetGreen), TargetBlue = unchecked((short)targetBlue) };
        return this with { Physical = words, Saved = words };
    }

    public bool IsFadeComplete => SyncMode == 2 ? Physical.Mode == 0 : Physical.Mode == 3 && Physical.Counter == 255;
    public bool IsColorComplete => Physical.Counter == Physical.Rate;

    public NativeFieldFadeState CompleteColorSync()
    {
        Validate();
        if (!IsColorComplete)
        {
            throw new InvalidOperationException("Native COLSYNC remains busy.");
        }
        return this with { Saved = Saved with { Mode = Physical.Mode } };
    }

    public NativeFieldFadeState ResumeKind6()
    {
        Validate();
        NativeFadeWords saved = Physical.Counter == Physical.Rate ? Saved with { Mode = 0 } : Saved;
        NativeFadeWords physical = saved.Mode != 0 ? saved : Physical with { Mode = 2, Counter = 255, Rate = 16, Red = 255, Green = 255, Blue = 255 };
        return this with { Physical = physical, Saved = saved };
    }

    public NativeFieldFadeState AdvanceCallback(bool interruptFade = false)
    {
        Validate();
        NativeFadeWords words = Physical;
        NativeFadeDraw draw = new(NativeFadeDrawMode.None, 0, 0, 0);
        switch (words.Mode)
        {
            case 0:
                words = words with { StartRed = 0, StartGreen = 0, StartBlue = 0 };
                break;
            case 2:
                short down = unchecked((short)(words.Counter - words.Rate));
                words = down <= 0 || interruptFade ? words with { Mode = 0, Counter = 0 } : words with { Counter = down };
                draw = ScaledDraw(words);
                break;
            case 3:
                short up = unchecked((short)(words.Counter + words.Rate));
                words = words with { Counter = up >= 256 ? (short)255 : up };
                draw = ScaledDraw(words);
                break;
            case 4:
                draw = new(NativeFadeDrawMode.Black, 0, 0, 0);
                break;
            case 5:
            case 6:
                short next = unchecked((short)(words.Counter + 1));
                words = next >= words.Rate ? words with { Counter = words.Rate, StartRed = words.TargetRed, StartGreen = words.TargetGreen, StartBlue = words.TargetBlue } : words with { Counter = next };
                words = words with { Red = Interpolate(words.Counter, words.Rate, words.StartRed, words.TargetRed), Green = Interpolate(words.Counter, words.Rate, words.StartGreen, words.TargetGreen), Blue = Interpolate(words.Counter, words.Rate, words.StartBlue, words.TargetBlue) };
                draw = new(words.Mode == 5 ? NativeFadeDrawMode.Add : NativeFadeDrawMode.Subtract, unchecked((byte)words.Red), unchecked((byte)words.Green), unchecked((byte)words.Blue));
                break;
            case 7:
            case 8:
                draw = new(words.Mode == 7 ? NativeFadeDrawMode.Add : NativeFadeDrawMode.Subtract, unchecked((byte)words.Red), unchecked((byte)words.Green), unchecked((byte)words.Blue));
                break;
        }
        return this with { Physical = words, Draw = draw };
    }

    private static short Interpolate(short counter, short duration, short start, short target) => unchecked((short)(start + (duration == 0 ? 0 : counter * unchecked((short)(target - start)) / duration)));
    private static NativeFadeDraw ScaledDraw(NativeFadeWords words) => new(NativeFadeDrawMode.Subtract, unchecked((byte)(words.Red * words.Counter / 256)), unchecked((byte)(words.Green * words.Counter / 256)), unchecked((byte)(words.Blue * words.Counter / 256)));

    public int ReadSaved(int address, int width)
    {
        ValidateRange(address, width);
        uint result = 0;
        for (int index = 0; index < width; index++)
        {
            int relative = address + index - 216;
            result |= (uint)(unchecked((ushort)Saved.At(relative / 2)) >> (8 * (relative & 1)) & 255) << (8 * index);
        }
        return unchecked((int)result);
    }

    public NativeFieldFadeState WriteSaved(int address, int width, int value)
    {
        ValidateRange(address, width);
        NativeFadeWords saved = Saved;
        for (int index = 0; index < width; index++)
        {
            int relative = address + index - 216;
            int shift = 8 * (relative & 1);
            int word = unchecked((ushort)saved.At(relative / 2));
            saved = saved.With(relative / 2, unchecked((short)(word & ~(255 << shift) | (value >> (8 * index) & 255) << shift)));
        }
        return this with { Saved = saved };
    }

    private void ValidateRange(int address, int width)
    {
        Validate();
        if (width is not (1 or 2 or 4) || address < 216 || (long)address + width > 240)
        {
            throw new NotSupportedException("Native fade memory access crosses its complete saved WORD bank.");
        }
    }
}
