namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeBlaster186Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - NativeBattleBlaster186Profile.RootBank);
        if (relative >= NativeBattleBlaster186Profile.RootBankLength || relative % NativeBattleBlaster186Profile.RootStride != 14)
        {
            return false;
        }
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleBlaster186Profile.Entry } current ? current : throw new NotSupportedException("Blaster completion requires its current physical root task.");
        updated = graphics with { OriginalEffect = NativeBattleBlaster186Controller.WriteCompletionByte(effect, address, value) };
        return true;
    }
}
