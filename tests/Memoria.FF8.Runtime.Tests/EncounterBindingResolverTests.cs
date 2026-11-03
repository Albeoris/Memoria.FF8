namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterBindingResolverTests
{
    [Fact]
    public void ResolvesOriginalBySceneIdentityAndAcceptsIndependentHpAndNameChanges()
    {
        BattleDefinition baseline = Definition();
        BattleDefinition modified = baseline with { Combatants = [baseline.Combatants[0] with { MaximumHp = 45000, Name = "Localized name" }, new() { Id = "another-enemy", ModelId = 88, SceneSlot = 1 }] };
        EncounterBindingResult result = EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, modified, new("Classic", 768, 136, 18));
        Assert.Equal("original", result.OriginalActorId);
        Assert.True(result.StoryRequestVerified);
        Assert.Equal(new byte[] { 8, 9, 10 }, result.TauntAbilities);
    }

    [Theory]
    [InlineData(120, 34)]
    [InlineData(136, 34)]
    [InlineData(120, 147)]
    public void RejectsLaterEncounterAndMismatchedStage(int encounter, int stage)
    {
        Assert.Throws<InvalidDataException>(() => EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, Definition() with { EncounterId = encounter, StageId = stage }, new("Classic")));
    }

    [Theory]
    [InlineData(768, 136, 19)]
    [InlineData(767, 136, 18)]
    [InlineData(768, 120, 18)]
    [InlineData(768, null, null)]
    public void RejectsConflictingOrPartialStoryContext(int field, int? encounter, int? flags)
    {
        Assert.Throws<InvalidDataException>(() => EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, Definition(), new("Classic", field, encounter, flags.HasValue ? checked((ushort)flags.Value) : null)));
    }

    [Fact]
    public void DiagnosticFormationDoesNotClaimStoryEntryProof()
    {
        EncounterBindingResult result = EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, Definition(), new("Classic"));
        Assert.False(result.StoryRequestVerified);
        Assert.Throws<InvalidDataException>(() => EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, Definition(), new("Remastered")));
    }

    [Fact]
    public void RejectsChangedTerminalContractWithoutGuardingNormalAttacksOrHp()
    {
        BattleDefinition baseline = Definition();
        NativeBattleAiDefinition ai = baseline.Combatants[0].NativeAi!;
        byte[][] scripts = ai.Scripts.Select(script => script.ToArray()).ToArray();
        scripts[3][3] = 10;
        BattleDefinition changed = baseline with { Combatants = [baseline.Combatants[0] with { NativeAi = ai with { Scripts = scripts } }] };
        Assert.Contains("terminal callback", Assert.Throws<InvalidDataException>(() => EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, changed, new("Classic"))).Message);
    }

    [Fact]
    public void TauntGuardRejectsChangedEligibilityWhileLeavingUnrelatedAttackBytesFree()
    {
        BattleDefinition baseline = Definition();
        NativeBattleAiDefinition ai = baseline.Combatants[0].NativeAi!;
        byte[][] scripts = ai.Scripts.Select(script => script.ToArray()).ToArray();
        scripts[1][20] = 0;
        scripts[1][21] = 0;
        BattleDefinition changed = baseline with { Combatants = [baseline.Combatants[0] with { NativeAi = ai with { Scripts = scripts } }] };
        Assert.Equal("original", EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, changed, new("Classic")).OriginalActorId);
        scripts[1][39] = 1;
        Assert.Contains("taunt branches", Assert.Throws<InvalidDataException>(() => EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, changed, new("Classic"))).Message);
    }

    private static BattleDefinition Definition()
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        abilities[8] = new(8, 13, 205);
        abilities[9] = new(8, 14, 206);
        abilities[10] = new(8, 15, 207);
        abilities[11] = new(8, 17, 264);
        byte[] normal = new byte[84];
        Write(0, [2, 220, 200, 0, 0, 0, 18, 0, 14, 220, 1, 4, 200, 12, 8]);
        Write(26, [2, 221, 200, 0, 0, 0, 22, 0, 2, 6, 200, 4, 2, 0, 11, 0, 14, 221, 1, 4, 200, 12, 9, 0]);
        Write(56, [2, 0, 0, 1, 10, 0, 19, 0, 2, 6, 200, 4, 2, 0, 8, 0, 4, 200, 12, 10, 0]);
        NativeBattleAiDefinition ai = new([[0], normal, [0], [4, 200, 12, 11, 0, 0], [0]], abilities, 0, []);
        return new() { EncounterId = 136, StageId = 147, Combatants = [new() { Id = "original", ModelId = 88, SceneSlot = 0, NativeAi = ai }] };

        void Write(int offset, byte[] instructions)
        {
            instructions.CopyTo(normal, offset);
        }
    }
}
