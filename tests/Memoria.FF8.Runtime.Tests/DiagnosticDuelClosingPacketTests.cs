namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticDuelClosingPacketTests
{
    [Fact]
    public void Closing_alias_cannot_be_observed_outside_its_actual_synchronous_native_owner()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 1 }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        BattleDiagnosticEffectDispatch closing = new(1, 1, "party", BattleAbility.NativeCommand, 65532, 274, 274, 241) { NativeActorIndex = 0, NativeHeaderIndex = 0, NativeHeaderGeneration = 1 };
        Assert.Throws<InvalidDataException>(() => battle.ObserveDiagnosticDuelClosingPackets(closing));
        Assert.Throws<InvalidOperationException>(() => battle.ObserveDiagnosticActionPackets(closing));
        Assert.Throws<ArgumentNullException>(() => battle.ObserveDiagnosticDuelClosingPackets(null!));
    }
}
