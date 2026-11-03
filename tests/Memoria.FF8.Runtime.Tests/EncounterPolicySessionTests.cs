using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterPolicySessionTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void NativeCallbackAdmitsOneMainActionAndEnemyDoubleCastsWithoutStocks(bool doubleActive, int expectedCasts)
    {
        BattleSession battle = Create(doubleActive);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(1ul, battle.EncounterPolicies!.Cursors["enemy"].CommittedTurns);
        Assert.Equal(expectedCasts, battle.Events.Count(entry => entry.Kind == "Scan" && entry.ActorId == "enemy"));
        Assert.Equal(expectedCasts, battle.NativeActionPackets.ActionCount);
        if (doubleActive)
        {
            Assert.Equal((byte)1, battle.NativeActionPackets.Headers[0].ReadByte(17));
        }
        Assert.Empty(battle.Combatants.Single(actor => actor.Id == "enemy").MagicStocks);
        Assert.Single(battle.ScanKnowledge.Observations);
    }

    [Fact]
    public void AdmittedMainActionCursorAndCompiledPolicySurviveJsonSnapshotRestore()
    {
        BattleSession battle = Create(true);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(battle.ReadDefinition().EncounterMod!.GenerationIdentity, restored.ReadDefinition().EncounterMod!.GenerationIdentity);
        Assert.Equal(1ul, restored.EncounterPolicies!.Cursors["enemy"].CommittedTurns);
        Assert.Equal(2ul, restored.EncounterPolicies.Cursors["enemy"].NextOwnTurn);
        Assert.Single(restored.ScanKnowledge.Observations);
    }

    private static BattleSession Create(bool doubleActive)
    {
        BattleNativeAction scan = new(2, 50, 1, 50)
        {
            MagicId = 1,
            AttackType = 12,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0x50,
            AttackFlags = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [0],
            SupplementalRecord = []
        };
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(2, 0, 50), 48).ToArray(), 0, []);
        BattleDefinition definition = new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = [scan],
            ActionDefinitions = [new(BattleAbility.Magic, 50, "Scan", BattleActionEffect.Native, 0, false) { NativeAction = scan }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ElementDefenses = [700, 800, 850, 900, 1000, 800, 800, 800] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 88, NativeAi = ai, NativeCombatantFlags = 0x11, InitialAtb = BattleSession.AtbMaximum, NativeStatus1 = doubleActive ? 0x20000u : 0 }
            ]
        };
        const string json = """
            {"schemaVersion":1,"documentId":"mod","encounter":"ff8.disc1.edea","atomic":true,"operations":[
              {"id":"policy","kind":"AddActionPolicy","policy":"policy","rules":[{"id":"scan","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent","casts":"nativeDouble"}}]},
              {"id":"assign","kind":"SetNormalActionPolicy","actor":"original","expectedOwner":"nativeNormalAction","policy":"policy","preserveNativeLifecycle":true}
            ]}
            """;
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new("ff8.disc1.edea", "Classic", ["original"]), [new("package", "gameplay/encounters/test.json", EncounterPatchDocument.Parse(json))]);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("ff8.disc1.edea", "enemy", 11, 11, [8, 9, 10], true));
        return new(definition with { EncounterMod = compiled }, BattleAtbMode.Normal);
    }
}
