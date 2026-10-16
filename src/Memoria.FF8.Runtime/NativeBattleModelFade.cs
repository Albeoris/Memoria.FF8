namespace Memoria.FF8.Runtime;

/// <summary>50C110 kind3 calls50C410 directly; completion retains the loaded allocation and does not start kind5 removal.</summary>
public static class NativeBattleModelFade
{
    public static NativeBattleGraphicsModel Advance50C410(NativeBattleGraphicsModel model, bool frozen, NativeBattleSummonColors colors)
    {
        if (model.Kind5 != 3)
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
}
