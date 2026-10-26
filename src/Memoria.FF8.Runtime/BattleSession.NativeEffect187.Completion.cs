using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect187Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: Entry } effect || address < ChildBank || address >= ChildBank + 36 * 100 || (address - ChildBank) % 36 != 14)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect187 completion requires its current process memory.");
        NativeBattleEffect187Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect187 completion requires its retained CRT stream."), memory.Effect187 ?? throw new NotSupportedException("Effect187 completion requires its actual retained task storage.")) { Staging = NativeFileStaging };
        frame = NativeBattleEffect187Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect187 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
