namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativePsychoBlast23Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        NativeBattleCureFamilyPool root = NativeBattlePsychoBlast23Profile.Pools[0];
        uint relative = unchecked(address - root.Bank);
        if (relative >= root.Stride * root.Capacity || relative % root.Stride != 0x62)
        {
            return false;
        }
        if (graphics.OriginalEffect is { Binding.Entry: NativeBattlePsychoBlast23Profile.Entry } effect)
        {
            updated = graphics with { OriginalEffect = WriteNativePsychoBlast23CompletionByte(effect, address, value) };
        }
        else if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattlePsychoBlast23Profile.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteNativePsychoBlast23CompletionByte(detached, address, value) };
        }
        else
        {
            throw new NotSupportedException("Psycho Blast texture completion requires its retained original root task.");
        }
        return true;
    }

    private static NativeBattleOriginalEffectState WriteNativePsychoBlast23CompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleCureFamilyPool root = NativeBattlePsychoBlast23Profile.Pools[0];
        uint relative = unchecked(address - root.Bank);
        if (effect.Binding.Entry != NativeBattlePsychoBlast23Profile.Entry || relative >= root.Stride * root.Capacity || relative % root.Stride != 0x62)
        {
            throw new NotSupportedException("Psycho Blast completion requires its original root completion byte.");
        }
        uint start = checked(address - 0x62);
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(start);
        if (task.Callback != NativeBattlePsychoBlast23Profile.RootCallback)
        {
            throw new InvalidDataException("Psycho Blast completion lost its actual retained root callback.");
        }
        return effect with { Pools = hierarchy.Write(start, task.WriteByte(0x62, value)).Pools };
    }
}
