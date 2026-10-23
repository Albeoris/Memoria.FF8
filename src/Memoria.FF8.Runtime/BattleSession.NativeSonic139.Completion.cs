namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeSonic139Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: NativeBattleSonic139Profile.Entry } effect)
        {
            return false;
        }
        NativeBattleEffectTaskPool? main = effect.Pools.FirstOrDefault(pool => pool.DescriptorAddress == NativeBattleSonic139Profile.MainDescriptor);
        if (main is null || address != main.BankAddress + 0x13)
        {
            return false;
        }
        updated = graphics with { OriginalEffect = NativeBattleSonic139Controller.WriteCompletionByte(effect, address, value) };
        return true;
    }
}
