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

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void EnemyPolicyReflectionPreservesCasterMainTurnAndRepeatCountAcrossSnapshot(bool doubleActive, int casts)
    {
        BattleSession battle = Create(doubleActive, true);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "Reflect"); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(casts, battle.Events.Count(entry => entry.Kind == "Reflect"));
        Assert.Equal(casts, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "enemy" && entry.TargetId == "enemy"));
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "party").CurrentHp);
        Assert.Equal(casts * 2, battle.NativeActionPackets.ActionCount);
        if (doubleActive)
        {
            Assert.Equal((byte)1, battle.NativeActionPackets.Headers[0].ReadByte(17));
        }
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp, restored.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal(1ul, restored.EncounterPolicies!.Cursors["enemy"].CommittedTurns);
        Assert.Empty(restored.Combatants.Single(actor => actor.Id == "enemy").MagicStocks);
        restored.Tick();
        Assert.Equal(casts, restored.Events.Count(entry => entry.Kind == "Reflect"));
    }

    [Fact]
    public void EnemyReflectedFreeMagicPacketSurvivesTheSharedInventorySnapshotValidator()
    {
        BattleDefinition source = Create(false, true).ReadDefinition();
        BattleDefinition definition = source with
        {
            EncounterMod = null,
            Combatants = source.Combatants.Select(actor => actor.Id == "enemy" ? actor with { NativeAi = null, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 1)] } : actor).ToArray()
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.QueueAction("enemy", "party", BattleAbility.Magic, 1);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "Reflect"); frame++)
        {
            battle.Tick();
        }
        Assert.Single(battle.Events, entry => entry.Kind == "Reflect");
        Assert.True(battle.QueuedActions.Any(command => command.NativeQueuedCommand == 247 && command.FreeNativeMagic) || battle.ActiveAction is { NativeQueuedCommand: 247, FreeNativeMagic: true });
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Single(restored.Events, entry => entry.Kind == "Damage" && entry.ActorId == "enemy" && entry.TargetId == "enemy");
        restored.Tick();
        Assert.Single(restored.Events, entry => entry.Kind == "Reflect");
    }
    [Fact]
    public void EnemyOrdinaryMagicCannotClaimThePlayerFreeMagicSnapshotFlag()
    {
        BattleSession battle = Create(false);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        BattleSnapshot snapshot = battle.Capture();
        Assert.True(snapshot.QueuedActions.Any(command => command.NativeQueuedCommand == 2) || snapshot.ActiveAction?.NativeQueuedCommand == 2);
        BattleSnapshot invalid = snapshot with
        {
            QueuedActions = snapshot.QueuedActions.Select(command => command.NativeQueuedCommand == 2 ? command with { FreeNativeMagic = true } : command).ToArray(),
            ActiveAction = snapshot.ActiveAction?.NativeQueuedCommand == 2 ? snapshot.ActiveAction with { FreeNativeMagic = true } : snapshot.ActiveAction
        };
        Assert.Contains("free magic", Assert.Throws<InvalidDataException>(() => BattleSession.Restore(invalid)).Message);
    }
    private static BattleSession Create(bool doubleActive, bool reflect = false)
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
        if (reflect)
        {
            scan = scan with { ActionId = 1, KernelIndex = 1, AttackType = 2, Power = 10, AttackFlags = 0x11 };
        }
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(2, 0, 50), 48).ToArray(), 0, []);
        BattleDefinition definition = new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = reflect ? [scan, scan with { Command = 247 }] : [scan],
            ActionDefinitions = [new(BattleAbility.Magic, scan.ActionId, reflect ? "Fire" : "Scan", BattleActionEffect.Native, 0, false) { NativeAction = scan }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, NativeStatus1 = reflect ? 0x80u : 0, ElementDefenses = [700, 800, 850, 900, 1000, 800, 800, 800] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 88, MaximumHp = 1000, CurrentHp = 1000, NativeAi = ai, NativeCombatantFlags = 0x11, InitialAtb = BattleSession.AtbMaximum, NativeStatus1 = doubleActive ? 0x20000u : 0 }
            ]
        };
        const string json = """
            {"schemaVersion":1,"documentId":"mod","encounter":"fixture.encounter","atomic":true,"operations":[
              {"id":"policy","kind":"AddActionPolicy","policy":"policy","rules":[{"id":"scan","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent","casts":"nativeDouble"}}]},
              {"id":"assign","kind":"SetNormalActionPolicy","actor":"original","expectedOwner":"nativeNormalAction","policy":"policy","preserveNativeLifecycle":true}
            ]}
            """;
        string payload = reflect ? json.Replace("\"Scan\"", "\"Fire\"", StringComparison.Ordinal) : json;
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new("fixture.encounter", "Classic", ["original"]), [new("package", "gameplay/encounters/test.json", EncounterPatchDocument.Parse(payload))]);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("fixture.encounter", "enemy", 11, 11, [8, 9, 10], true));
        return new(definition with { EncounterMod = compiled }, BattleAtbMode.Normal);
    }
}
