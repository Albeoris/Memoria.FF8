using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect326Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: Entry } effect || address < ChildBank || address >= ChildBank + 36 * 100 || (address - ChildBank) % 36 != 14)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect326 completion requires its current process memory.");
        NativeBattleEffect326Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect326 completion requires its retained CRT stream."), memory.Effect326 ?? throw new NotSupportedException("Effect326 completion requires its actual retained task storage.")) { Staging = NativeFileStaging };
        frame = NativeBattleEffect326Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect326 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
