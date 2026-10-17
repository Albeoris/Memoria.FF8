namespace Memoria.FF8.Runtime;

/// <summary>50C410/50C560 color and alpha writes, shared by direct fades and deferred removal services.</summary>
public static class NativeBattleModelFade
{
    public static NativeBattleGraphicsModel Advance50C410(NativeBattleGraphicsModel model, bool frozen, NativeBattleSummonColors colors)
    {
        if (model.Kind5 is not (3 or 5 or 6))
        {
            return model;
        }
        byte phase = model.Phase6 ?? throw new InvalidDataException("Native50C410 requires its actual phaseBYTE6.");
        if (phase >= 15)
        {
            ushort flags = model.Flags2 ?? throw new InvalidDataException("Native50C410 requires its model Flags2.");
            model = model with { Flags0 = (ushort)(model.Flags0 | 4), Flags2 = (ushort)(flags | 0x80), Color40 = 0, Kind5 = 0 };
        }
        else
        {
            int progress = (phase << 12) / 15;
            int colorFactor = 4096 - NativeBattleCameraMath.Cosine(progress / 4);
            model = model with
            {
                Color40 = (colors.Interpolate(colors.Transition, colorFactor, 0) & 0xFFFFFFu) | 0x02000000u,
                Color44 = (colors.Interpolate(colors.Normal, progress, 0) & 0xFFFFFFu) | 0x32000000u,
                Alpha7 = unchecked((byte)((-255 * progress >> 12) - 1))
            };
        }
        return model with { Phase6 = frozen ? phase : unchecked((byte)(phase + 1)) };
    }

    public static NativeBattleGraphicsModel Advance50C560(NativeBattleGraphicsModel model, bool frozen, NativeBattleSummonColors colors)
    {
        if (model.Kind5 is not (4 or 7))
        {
            return model;
        }
        byte phase = model.Phase6 ?? throw new InvalidDataException("Native50C560 requires its actual phaseBYTE6.");
        if (phase >= 15)
        {
            ushort flags = model.Flags2 ?? throw new InvalidDataException("Native50C560 requires its model Flags2.");
            model = model with { Flags0 = (ushort)(model.Flags0 | 4), Flags2 = (ushort)(flags | 0x80), Color40 = 0, Kind5 = 0 };
        }
        else
        {
            uint source = colors.LinearFadeSourceB8BA94 ?? throw new NotSupportedException("Native50C560 requires its authenticated source color DWORD; old missing rules are unknown.");
            int progress = (phase << 12) / 15;
            model = model with { Color40 = (colors.Interpolate(source, progress, 0) & 0xFFFFFFu) | 0x02000000u, Color44 = (colors.Interpolate(colors.Normal, progress, 0) & 0xFFFFFFu) | 0x32000000u };
        }
        return model with { Phase6 = frozen ? phase : unchecked((byte)(phase + 1)) };
    }

    public static NativeBattleGraphicsModel AdvanceFlash50C670(NativeBattleGraphicsModel model, NativeBattleSummonColors colors, Func<ushort> random)
    {
        if (model.Kind5 != 8)
        {
            return model;
        }
        byte phase = model.Phase6 ?? throw new InvalidDataException("Native50C670 requires its actual phaseBYTE6.");
        if (phase < 2)
        {
            uint current = model.Color40 ?? throw new NotSupportedException("Native50C670 requires its retained model color DWORD40.");
            model = model with { Color40 = colors.Interpolate(current, (phase + 1) << 11, 0xFFFFFFu) & 0xFFFFFFu };
        }
        return model with { Phase6 = phase == 0 ? (byte)(random() % 15 + 1) : (byte)(phase - 1) };
    }
}
