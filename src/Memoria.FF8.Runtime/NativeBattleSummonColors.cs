namespace Memoria.FF8.Runtime;

/// <summary>Authenticated B8B7D8/B8B9A8 colors and45F270 fixed-point interpolation constants.</summary>
public sealed record NativeBattleSummonColors(uint Transition, uint Normal, float FixedScale, double IntegerBias)
{
    public uint? LinearFadeSourceB8BA94 { get; init; }
    public uint Interpolate(uint source, int factor, uint farColor)
    {
        uint result = source & 0xFF000000u;
        for (int shift = 0; shift < 24; shift += 8)
        {
            int initial = 16 * (byte)(source >> shift);
            int far = 16 * (byte)(farColor >> shift);
            double interpolated = (far - initial) * ((short)factor * (double)FixedScale) + IntegerBias;
            int value = unchecked((int)((uint)BitConverter.DoubleToInt64Bits(interpolated) + (uint)initial + 0x80000000u)) >> 4;
            result |= (uint)Math.Clamp(value, 0, 255) << shift;
        }
        return result;
    }
}

public static class NativeBattleSummonModels
{
    /// <summary>50C110 kinds11/13→50C4D0 and10/12→50C330, with distinct pause/increment order.</summary>
    public static NativeBattleGraphicsModel Advance(NativeBattleGraphicsModel model, bool frozen, NativeBattleSummonColors colors)
    {
        if (model.Kind5 is not (10 or 11 or 12 or 13))
        {
            return model;
        }
        byte phase = model.Phase6 ?? throw new InvalidDataException("Native summon model service requires phaseBYTE6.");
        bool removeAfterFade = model.Kind5 == 13;
        if (model.Kind5 is 11 or 13)
        {
            if (phase >= 15)
            {
                ushort flags = model.Flags2 ?? throw new InvalidDataException("Native50C4D0 lacks its model Flags2.");
                model = model with { Flags0 = (ushort)(model.Flags0 | 4), Flags2 = (ushort)(flags | 0x80), Color40 = 0, Kind5 = 0 };
            }
            else
            {
                int progress = (phase << 12) / 15;
                model = model with { Color40 = (colors.Interpolate(colors.Transition, progress, 0) & 0xFFFFFFu) | 0x02000000u, Color44 = (colors.Interpolate(colors.Normal, progress, 0) & 0xFFFFFFu) | 0x32000000u, Alpha7 = 0 };
            }
        }
        else
        {
            phase = unchecked((byte)(phase + 1));
            if (phase >= 15)
            {
                model = model with { Color44 = (colors.Normal & 0xFFFFFFu) | 0x32000000u, Alpha7 = 0, Kind5 = 0 };
            }
            else
            {
                int progress = (phase << 12) / 15;
                uint current = model.Color40 ?? throw new NotSupportedException("Native50C330 requires the retained model colorDWORD40.");
                model = model with { Color40 = (colors.Interpolate(0, progress, current) & 0xFFFFFFu) | 0x02000000u, Color44 = (colors.Interpolate(0, progress, colors.Normal) & 0xFFFFFFu) | 0x32000000u, Alpha7 = 0 };
            }
        }
        model = model with { Phase6 = frozen ? phase : unchecked((byte)(phase + 1)) };
        return removeAfterFade && model.Kind5 == 0 ? NativeBattleGraphicsModels.StartService(model, 5) : model;
    }
}
