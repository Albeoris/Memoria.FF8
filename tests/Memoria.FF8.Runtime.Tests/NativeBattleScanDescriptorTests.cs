using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanDescriptorTests
{
    [Theory]
    [InlineData(true, 0, 0)]
    [InlineData(true, 4, 4)]
    [InlineData(false, 0, 16)]
    [InlineData(false, 61, 77)]
    [InlineData(false, 88, 104)]
    [InlineData(false, 143, 159)]
    public void Scan_descriptor_uses_the_allocated_model_identity(bool player, int model, byte expected)
    {
        BattleSession battle = new(new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = player ? model : 0 },
                new() { Id = "enemy", Name = "Enemy", ModelId = player ? 0 : model }
            ]
        }, BattleAtbMode.Normal);
        int index = battle.Combatants.Single(actor => actor.IsPlayer == player).NativeIndex;
        MethodInfo read = typeof(BattleSession).GetMethod("ReadNativeScanDescriptor208", BindingFlags.Instance | BindingFlags.NonPublic)!;
        NativeBattleScanByteOwner descriptor = Assert.IsType<NativeBattleScanByteOwner>(read.Invoke(battle, [index]));
        Assert.Equal(expected, descriptor.ReadByte(0xBB));
    }
}
