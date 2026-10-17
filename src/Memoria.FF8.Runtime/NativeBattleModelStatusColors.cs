namespace Memoria.FF8.Runtime;

/// <summary>50A1C0's per-model status colors, using the original DWORD8 priority and BYTE timers behind DWORD88.</summary>
public static class NativeBattleModelStatusColors
{
    public static NativeBattleGraphicsModel Advance50A1C0(NativeBattleGraphicsModel model, NativeBattleSummonColors colors)
    {
        uint flags = model.Flags8 ?? throw new NotSupportedException("Native50A1C0 requires the retained model status DWORD8.");
        if ((flags & 0x10) != 0)
        {
            byte modelId = model.ModelId4 ?? throw new NotSupportedException("Native50A1C0 petrification requires the original model BYTE4.");
            if (modelId < 16)
            {
                return model with { Color40 = colors.Transition };
            }
            uint current = model.Color40 ?? throw new NotSupportedException("Native50A1C0 petrification preserves the retained color command BYTE2B.");
            return model with { Color40 = (current & 0xFF000000u) | 0xFFFFFFu };
        }
        if ((flags & 0x100000) != 0)
        {
            return model with { Color40 = (colors.Transition & 0xFFFFFFu) | 0x02000000u, Alpha7 = 0 };
        }
        if ((flags & 0x2060500) == 0)
        {
            return model;
        }

        int timerIndex;
        uint farColor;
        if ((flags & 0x400) != 0)
        {
            byte modelId = model.ModelId4 ?? throw new NotSupportedException("Native50A1C0 poison color requires the original model BYTE4.");
            if (modelId < 16)
            {
                return model with { Color40 = colors.Transition };
            }
            timerIndex = 10;
            farColor = 0x004000;
        }
        else if ((flags & 0x100) != 0)
        {
            timerIndex = 18;
            farColor = 0x2020C0;
        }
        else if ((flags & 0x20000) != 0)
        {
            timerIndex = 17;
            farColor = 0x00C0FF;
        }
        else if ((flags & 0x40000) != 0)
        {
            timerIndex = 18;
            farColor = 0x202020;
        }
        else
        {
            timerIndex = 25;
            farColor = 0xC060A0;
        }
        if (model.StatusTimers.IsDefault || model.StatusTimers.Length <= timerIndex || model.StatusTimers[timerIndex] is not byte timer)
        {
            throw new NotSupportedException($"Native50A1C0 requires the retained status timer BYTE{timerIndex:X2}.");
        }
        int angle = ((timer << 12) / 32 - 1024) & 0xFFF;
        int factor = (NativeBattleCameraMath.Sine(angle) + 4096) / 4 + 2048;
        uint color = colors.Interpolate(colors.Transition, factor, farColor) & 0xFFFFFFu;
        return model with { Color40 = color, StatusTimers = model.StatusTimers.SetItem(timerIndex, unchecked((byte)(timer + 1))) };
    }
}
