using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.App.Tests;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectComponentPreviewTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void Raw_component_private_handoff_retains_declared_scheduling_through_natural_gauge_readiness(ushort effect)
    {
        string directory = TestResources.GameDirectory;
        GameSession producerSession = GameWorld.CreateColdProcess(directory);
        producerSession.StartNewGame();
        using GameWorld producer = new(producerSession, directory);
        BattleSession source = producer.StartEffectPreview(new(0, 0, effect));
        source.PublishDiagnosticEffectPreview(effect);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(admission)));
        BattleSession.DiagnosticTransportContinuation declared = Assert.IsType<BattleSession.DiagnosticTransportContinuation>(source.CaptureForContinuation().LogicalContinuation!.DiagnosticTransport);
        Assert.True(declared.HoldEnemyCommands);
        Assert.True(declared.EffectPreview);
        Assert.NotNull(declared.PreviewToken);
        BattleSnapshot invalid = source.CaptureForContinuation();
        invalid = invalid with { LogicalContinuation = invalid.LogicalContinuation! with { DiagnosticTransport = declared with { ReadinessActorId = "missing-owner" } } };
        BattleSession rejected = BattleSession.Restore(invalid);
        Assert.Throws<InvalidDataException>(() => rejected.AttachRestoredPresentation(invalid.PresentationContinuation!.Resources, invalid));
        string before = JsonSerializer.Serialize(source.Capture());
        int sourceDispatches = 0;
        source.DiagnosticEffectDispatched += _ => sourceDispatches++;
        GameSession session = GameWorld.CreateColdProcess(directory);
        using GameWorld world = new(session, directory);
        BattleSession active = world.StartEffectPreview(new(0, 0, effect), new(source, hash));
        Assert.Equal(declared, active.CaptureForContinuation().LogicalContinuation!.DiagnosticTransport);
        BattleCombatant enemy = Assert.Single(active.Combatants, actor => !actor.IsPlayer);
        Assert.Null(enemy.NativeAi);
        for (int frame = 0; frame < 1800; frame++)
        {
            active.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
            if (enemy.NativeAtb!.Current >= enemy.NativeAtb.Maximum)
            {
                break;
            }
        }
        Assert.Equal(enemy.NativeAtb!.Maximum, enemy.NativeAtb.Current);
        Assert.Equal(0, sourceDispatches);
        Assert.Equal(before, JsonSerializer.Serialize(source.Capture()));
    }

    [Fact]
    public void Private_fork_retains_the_transport_settings_declared_by_its_source()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        BattleSession source = world.StartDiagnosticBattle(new(462, 4, 0x80, 0, 0) { BattleMusic = 5, PrimaryMusicChannel = -1, Fixture = new([], [], false) });
        BattleSession.DiagnosticTransportContinuation declared = Assert.IsType<BattleSession.DiagnosticTransportContinuation>(source.CaptureForContinuation().LogicalContinuation!.DiagnosticTransport);
        Assert.False(declared.HoldEnemyCommands);
        Assert.True(declared.InstantGuardianForceCharge);
        Assert.False(declared.EffectPreview);
        Assert.Equal(declared, source.ForkForContinuation().CaptureForContinuation().LogicalContinuation!.DiagnosticTransport);
    }

    [Fact]
    public void Effect_preview_json_rejects_legacy_live_source_fields()
    {
        const string legacy = """
            {"EnemyId":1,"AllyId":0,"EffectId":219,"SourceSnapshot":"battle.json","SourceSha256":"ignored"}
            """;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EffectPreviewRequest>(legacy));
    }

    [Fact]
    public void Battle_preview_rejects_legacy_snapshot_argument()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => BattlePreview.Run(["--snapshot", "battle.json", "--output", "battle.png"]));
        Assert.Contains("Unknown battle preview argument '--snapshot'", error.Message);
    }

    [Fact]
    public void Original_enemy_component_rebinds_its_typed_action_and_preserves_the_next_callback()
    {
        string directory = TestResources.GameDirectory;
        GameSession producerSession = GameWorld.CreateColdProcess(directory);
        producerSession.StartNewGame();
        using GameWorld producer = new(producerSession, directory);
        BattleSession source = PrepareOriginalEnemyComponent(producer);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 219);
        Assert.True(admission.ModuleLoaded);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(admission)));
        BattleSession expected = source.ForkForContinuation();
        string initial = JsonSerializer.Serialize(source.Capture());
        GameSession session = GameWorld.CreateColdProcess(directory);
        using GameWorld world = new(session, directory);

        BattleSession active = world.StartEffectPreview(new(1, 0, 219), new(source, hash));

        Assert.True(world.IsEffectComponentPreview);
        Assert.Same(active, world.Battle);
        Assert.NotSame(source.Presentation, active.Presentation);
        Assert.Equal(admission, GameWorld.ValidateEffectComponentSource(active, 219));
        Assert.Throws<InvalidDataException>(() => GameWorld.ValidateEffectComponentSource(active, 220));
        Assert.Throws<InvalidOperationException>(() => active.PublishDiagnosticEffectPreview(219));
        using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        active.Tick();
        expected.Tick();
        world.SynchronizeBattlePresentation();
        Assert.Equal(JsonSerializer.Serialize(expected.Capture()), JsonSerializer.Serialize(active.Capture()));
        Assert.Equal(initial, JsonSerializer.Serialize(source.Capture()));
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Null(world.LastBattleRewards);
    }

    internal static BattleSession PrepareOriginalEnemyComponent(GameWorld world)
    {
        const ushort effect = 219;
        const ushort formation = 462;
        BattleSession battle = world.StartDiagnosticBattle(new(formation, 4, 0x80, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            AtbMode = BattleAtbMode.Normal,
            Fixture = new([], [], true)
        });
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.NativeActorIndex == 3);
        NativeBattleAiDefinition ai = Assert.IsType<NativeBattleAiDefinition>(actor.NativeAi);
        var selected = Assert.Single(ai.Abilities.Skip(16 * ai.LevelBand).Take(16).Select((row, slot) => (Row: row, Slot: slot)),
            entry => entry.Row.Command == 8 && world.BattleResources.NativeActions.Any(action => action.Command == entry.Row.Command && action.ActionId == entry.Row.Id && action.MagicId == effect));
        BattleDiagnosticEnemyAbility ability = new(actor.ModelId, actor.SceneSlot!.Value, ai.LevelBand, selected.Slot, selected.Row.Command, selected.Row.Id, selected.Row.Sequence, 201);
        for (int callback = 0; callback < 1200 && !battle.CanSubmitDiagnosticEnemyAbility(actor.Id, ability); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanSubmitDiagnosticEnemyAbility(actor.Id, ability));
        battle.SubmitDiagnosticEnemyAbility(actor.Id, ability, effect);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int callback = 0; callback < 360 && process.ActiveActionToken is null; callback++)
        {
            battle.Tick();
        }
        _ = GameWorld.ValidateEffectComponentSource(battle, effect);
        return battle;
    }
}
