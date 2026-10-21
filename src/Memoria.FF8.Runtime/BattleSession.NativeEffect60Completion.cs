namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect60Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        NativeBattleCureFamilyPool root = NativeBattleEffect60Profile.Pools[0];
        uint relative = unchecked(address - root.Bank);
        if (relative >= root.Stride * root.Capacity || relative % root.Stride != 0x62)
        {
            return false;
        }
        if (graphics.OriginalEffect is { Binding.Entry: NativeBattleEffect60Profile.Entry } effect)
        {
            updated = graphics with { OriginalEffect = WriteNativeEffect60CompletionByte(effect, address, value) };
        }
        else if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleEffect60Profile.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteNativeEffect60CompletionByte(detached, address, value) };
        }
        else
        {
            throw new NotSupportedException("Effect60 texture completion requires its retained original root task.");
        }
        return true;
    }

    private static NativeBattleOriginalEffectState WriteNativeEffect60CompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleCureFamilyPool root = NativeBattleEffect60Profile.Pools[0];
        uint relative = unchecked(address - root.Bank);
        if (effect.Binding.Entry != NativeBattleEffect60Profile.Entry || relative >= root.Stride * root.Capacity || relative % root.Stride != 0x62)
        {
            throw new NotSupportedException("Effect60 completion requires its original root completion byte.");
        }
        uint start = checked(address - 0x62);
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(start);
        if (task.Callback != NativeBattleEffect60Profile.RootCallback)
        {
            throw new InvalidDataException("Effect60 completion lost its actual retained root callback.");
        }
        return effect with { Pools = hierarchy.Write(start, task.WriteByte(0x62, value)).Pools };
    }
}
