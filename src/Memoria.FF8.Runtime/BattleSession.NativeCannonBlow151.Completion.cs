namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeCannonBlow151Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - NativeBattleCannonBlow151Profile.RootBank);
        if (relative >= NativeBattleCannonBlow151Profile.RootBankLength || relative % NativeBattleCannonBlow151Profile.RootStride != 14)
        {
            return false;
        }
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleCannonBlow151Profile.Entry } current ? current : throw new NotSupportedException("Cannon Blow completion requires its current physical root task.");
        updated = graphics with { OriginalEffect = NativeBattleCannonBlow151Controller.WriteCompletionByte(effect, address, value) };
        return true;
    }
}
