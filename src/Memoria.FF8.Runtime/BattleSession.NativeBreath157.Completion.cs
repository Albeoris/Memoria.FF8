namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeBreath157Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - NativeBattleBreath157Profile.RootBank);
        if (relative >= NativeBattleBreath157Profile.RootBankLength || relative % NativeBattleBreath157Profile.RootStride != 14)
        {
            return false;
        }
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleBreath157Profile.Entry } current ? current : throw new NotSupportedException("Breath completion requires its current physical root task.");
        updated = graphics with { OriginalEffect = NativeBattleBreath157Controller.WriteCompletionByte(effect, address, value) };
        return true;
    }
}
