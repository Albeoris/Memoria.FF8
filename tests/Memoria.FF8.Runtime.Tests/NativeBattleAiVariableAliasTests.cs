namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiVariableAliasTests
{
    [Fact]
    public void NativeActorThreeVariable96WritesThePartyResistanceDwordAndRestoresIt()
    {
        BattleSession battle = Create([14, 96, 1, 0]);
        BattleSnapshot snapshot = battle.Capture();

        Assert.Equal(new byte[] { 1, 0, 0, 0 }, snapshot.Combatants[0].NativeStatus!.Resistances![20..24]);
        Assert.All(snapshot.Definition.Combatants[0].NativeStatusResistances, value => Assert.Equal(100, value));
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(snapshot.Combatants[0].NativeStatus!.Resistances, restored.Capture().Combatants[0].NativeStatus!.Resistances);
    }

    [Fact]
    public void NativeActorAliasAdditionCarriesAcrossAllFourBytesAndWrapsUnsigned()
    {
        BattleSession battle = Create([18, 96, 1, 0], 255);

        Assert.Equal(new byte[4], battle.Capture().Combatants[0].NativeStatus!.Resistances![20..24]);
    }

    [Fact]
    public void NativeActorAliasCanAddressAnotherActorsOwnedLocalVariables()
    {
        //Actor3+4*(64-211) is party0+36, the first local DWORD.
        BattleSession battle = Create([14, 64, 42, 0]);

        Assert.Equal(42u, battle.Capture().Combatants[0].NativeParticipant!.Variables[0]);
    }

    [Fact]
    public void NativeActorAliasesRejectUnownedMemoryAndMissingResistanceState()
    {
        Assert.Contains("unowned actor memory", Assert.Throws<NotSupportedException>(() => Create([14, 95, 1, 0], known: false)).Message);
        BattleSnapshot snapshot = Create([0]).Capture();
        snapshot = snapshot with { Combatants = snapshot.Combatants.Select((actor, index) => index == 0 ? actor with { NativeStatus = actor.NativeStatus! with { Resistances = null } } : actor).ToArray() };

        Assert.Contains("resistance bank", Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot)).Message);
    }

    private static BattleSession Create(byte[] init, byte resistance = 100, bool known = true)
    {
        NativeBattleAiDefinition ai = new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, NativeStatusResistances = known ? Enumerable.Repeat(resistance, 40).ToArray() : [] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 71, NativeAi = ai }
            ]
        }, BattleAtbMode.Normal);
        battle.Tick();
        return battle;
    }
}
