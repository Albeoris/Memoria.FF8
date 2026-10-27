using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native message-channel openness, glyph speed accumulator and text runner.</summary>
public sealed record NativeWorldTextState([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] int Position, [property: JsonRequired] short Speed, [property: JsonRequired] short Accumulator, [property: JsonRequired] short Openness, [property: JsonRequired] int Phase, [property: JsonRequired] byte PauseTicks, [property: JsonRequired] byte PageCounter, [property: JsonRequired] bool TextComplete)
{
    public static NativeWorldTextState Create(ImmutableArray<byte> bytes, short speed)
    {
        NativeWorldTextState state = new(bytes, 0, speed, 0, 0, 0, 0, 0, false);
        state.Validate();
        return state;
    }

    public void Validate()
    {
        if (Bytes.IsDefaultOrEmpty || Bytes[^1] != 0 || Position < 0 || Position >= Bytes.Length || Openness is < 0 or > 4096 || Phase is not (0 or 1 or 7 or 9 or 10 or 15) || TextComplete != (Phase == 7) || TextComplete && Bytes[Position] != 0)
        {
            throw new InvalidDataException("World text requires its bounded native stream and channel runner state.");
        }
        int lineBytes = 0;
        for (int index = 0; index < Bytes.Length; index++)
        {
            byte code = Bytes[index];
            if (code is 0 or 1 or 2 or 7)
            {
                if (lineBytes > 127)
                {
                    throw new NotSupportedException("World text line requires native buffer truncation beyond its 128-byte expanded line buffer.");
                }
                lineBytes = 0;
            }
            else
            {
                lineBytes++;
            }
            if (code == 0)
            {
                if (index != Bytes.Length - 1)
                {
                    throw new InvalidDataException("World text has bytes after its terminator.");
                }
            }
            else if (code is 5 or 6 or 8 or 9)
            {
                lineBytes++;
                if (++index >= Bytes.Length)
                {
                    throw new InvalidDataException("World text control requires its complete parameter.");
                }
                if (code == 8 && Bytes[index] == 0)
                {
                    if (index != Bytes.Length - 1)
                    {
                        throw new InvalidDataException("World text has bytes after its speed-control terminator.");
                    }
                }
                else if (index >= Bytes.Length - 1 || Bytes[index] < 32)
                {
                    throw new InvalidDataException("World text control requires its original nonzero parameter.");
                }
            }
            else if (code is not (1 or 2 or 7) && (code < 32 || code >= 232))
            {
                throw new NotSupportedException($"World text token0x{code:X2} requires its executed native substitution or multibyte owner.");
            }
        }
    }

    public NativeWorldTextState Advance(uint currentButtons = 0)
    {
        Validate();
        NativeWorldTextState state = this with { Openness = (short)Math.Min(4096, Openness + 512) };
        if (state.Openness < 4096 || state.TextComplete)
        {
            return state;
        }
        int step = state.Phase;
        while (true)
        {
            switch (step)
            {
                case 0:
                    state = state with { Phase = 1 };
                    step = 1;
                    break;
                case 1:
                    state = state with { Accumulator = unchecked((short)(state.Accumulator + state.Speed)) };
                    step = 2;
                    break;
                case 2:
                    if (state.Speed != 0 && state.Accumulator < 4096)
                    {
                        return state;
                    }
                    state = state with { Accumulator = unchecked((short)(state.Accumulator - 4096)) };
                    step = 3;
                    break;
                case 3:
                    byte code = state.Bytes[state.Position];
                    if (code is 1 or 7)
                    {
                        return state with { Phase = 9, PageCounter = (byte)((state.PageCounter & 127) | (code == 7 ? 128 : 0)) };
                    }
                    if (code == 0)
                    {
                        return state with { Phase = 7, TextComplete = true };
                    }
                    state = state with { Position = state.Position + 1 };
                    if (code == 2)
                    {
                        // Automatically measured world windows fit the largest authored page.
                        step = 3;
                    }
                    else if (code is 5 or 6 or 8 or 9)
                    {
                        byte parameter = state.Bytes[state.Position];
                        if (code == 8 && parameter == 0)
                        {
                            return state with { Phase = 7, TextComplete = true };
                        }
                        state = state with { Position = state.Position + 1 };
                        if (code == 8)
                        {
                            short speed = parameter == 32 ? (short)4096 : parameter == 33 ? (short)0 : checked((short)(4096 / (parameter - 33)));
                            state = state with { Speed = speed };
                            step = 3;
                        }
                        else if (code == 9)
                        {
                            state = state with { Phase = 15, PauseTicks = (byte)(parameter - 32) };
                            step = 15;
                        }
                        else
                        {
                            step = code == 6 || state.Speed == 0 ? 3 : 2;
                        }
                    }
                    else
                    {
                        step = state.Speed == 0 ? 3 : 2;
                    }
                    break;
                case 9:
                    state = state with { PageCounter = unchecked((byte)((state.PageCounter & 128) | ((state.PageCounter + 1) & 127))) };
                    if ((currentButtons & 0xFFFF0FFF) == 0)
                    {
                        state = state with { Phase = 10 };
                    }
                    return state;
                case 10:
                    state = state with { PageCounter = unchecked((byte)((state.PageCounter & 128) | ((state.PageCounter + 1) & 127))) };
                    if ((currentButtons & 192) != 0)
                    {
                        state = state with { Phase = 1, PageCounter = (byte)(state.PageCounter & 127), Position = state.Position + 1 };
                    }
                    return state;
                case 15:
                    if (state.PauseTicks != 0)
                    {
                        return state with { PauseTicks = (byte)(state.PauseTicks - 1) };
                    }
                    state = state with { Phase = 1 };
                    step = 2;
                    break;
                default:
                    throw new InvalidDataException("World text reached an unsupported native channel phase.");
            }
        }
    }
}
