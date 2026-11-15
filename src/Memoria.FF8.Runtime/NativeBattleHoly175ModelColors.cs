namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175ModelColorStep(NativeBattleModulePoolTask Task, BattleActorModels Models, int Result);

/// <summary>5D27C0/5D3030's model color writes before pause, followed by their signed counter and visibility transitions.</summary>
public static class NativeBattleHoly175ModelColors
{
    public static NativeBattleHoly175ModelColorStep Advance(NativeBattleModulePoolTask task, BattleActorModels models, uint graphicsFlags, uint currentNormal, uint currentTransition, NativeBattleSummonColors colors)
    {
        bool fadeIn = task.Callback == 0x5D27C0;
        if (!fadeIn && task.Callback != 0x5D3030)
        {
            throw new NotSupportedException("Native Holy model colors require their actual callback.");
        }
        BattleModelOwner owner = task.ReadExecutionReference<BattleExecutionReference.Model>(0xE, 2).Owner;
        NativeBattleGraphicsModel selected = models.Get(owner);
        int duration = unchecked((short)task.ReadWord(0x18));
        if (duration == 0)
        {
            throw new InvalidDataException("Native Holy model color duration cannot divide by zero.");
        }
        int counter = unchecked((short)task.ReadWord(0xC));
        int quotient = unchecked(counter << 12) / duration;
        int factor = fadeIn ? quotient : unchecked(4096 - quotient);
        uint halfTransition = (currentTransition & 0x00FEFEFEu) >> 1;
        NativeBattleGraphicsModel model = selected with
        {
            Color44 = colors.Interpolate(0x32000000, factor, currentNormal),
            Color40 = (colors.Interpolate(0, factor, halfTransition) & 0xFFFFFFu) | 0x02000000u
        };
        if (!fadeIn)
        {
            model = model with { Alpha7 = 0 };
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, models.Set(owner, model), 0);
        }
        if (counter == 0)
        {
            model = model with { Flags0 = unchecked((ushort)(fadeIn ? (model.Flags0 & 0xFFFB) | 0x800 : model.Flags0 | 0x800)) };
        }
        task = task.WriteWord(0xC, unchecked((ushort)(counter + 1)));
        if (unchecked((short)task.ReadWord(0xC)) < duration)
        {
            return new(task, models.Set(owner, model), 0);
        }
        model = model with { Flags0 = unchecked((ushort)((model.Flags0 & 0xF7FF) | task.ReadWord(0x1A) | (fadeIn ? 0 : 4))) };
        if (fadeIn)
        {
            model = model with { Alpha7 = 0, Color40 = currentTransition };
        }
        return new(task, models.Set(owner, model), 2);
    }
}
