namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeWildCannonBlow156Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - NativeBattleWildCannonBlow156Profile.RootBank);
        if (relative >= NativeBattleWildCannonBlow156Profile.RootBankLength || relative % NativeBattleWildCannonBlow156Profile.RootStride != 14)
        {
            return false;
        }
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleWildCannonBlow156Profile.Entry } current ? current : throw new NotSupportedException("Wild Cannon Blow completion requires its current physical root task.");
        updated = graphics with { OriginalEffect = NativeBattleWildCannonBlow156Controller.WriteCompletionByte(effect, address, value) };
        return true;
    }
}
