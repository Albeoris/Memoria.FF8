namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect315Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - NativeBattleEffect315Profile.RootBank);
        if (relative >= NativeBattleEffect315Profile.RootBankLength || relative % NativeBattleEffect315Profile.RootStride != 0xE)
        {
            return false;
        }
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleEffect315Profile.Entry } current ? current : throw new NotSupportedException("Effect315 completion requires its current physical root task.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect315 completion requires its current process memory.");
        NativeBattleEffect315Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect315 completion requires its current CRT owner."), memory.Effect315 ?? throw new NotSupportedException("Effect315 completion requires its current executable storage."))
        {
            Staging = NativeFileStaging ?? throw new NotSupportedException("Effect315 completion requires its current temporary owner.")
        };
        frame = NativeBattleEffect315Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect315 = frame.State };
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        updated = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
