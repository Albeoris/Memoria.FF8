namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect140Completion(uint address, byte value)
    {
        if (address != 0x2517B24)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect140 completion requires its current process memory.");
        NativeBattleEffect140State state = memory.Effect140 ?? throw new NotSupportedException("Effect140 completion requires its retained storage.");
        NativeEffectMemory = memory with { Effect140 = state.Write(address, [value]) };
        return true;
    }
}
