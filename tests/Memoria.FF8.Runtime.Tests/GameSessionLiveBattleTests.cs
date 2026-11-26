using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameSessionLiveBattleTests
{
    [Fact]
    public void LivePublicationDefersPersistenceAndExportsRetainedMemoryAtReturn()
    {
        const uint stagingAddress = 0x1D999C0;
        const byte marker = 33;
        BattleSession battle = CreateBattle();
        battle.Tick();
        GameSession validated = GameSession.CreateColdProcess();
        GameSession live = GameSession.CreateColdProcess();

        validated.SetBattle(battle.Capture());
        live.SetLiveBattle(battle);
        NativeBattleRetainedMemory beforeReturn = live.State.NativeBattleMemory!;
        byte prior = beforeReturn.FileStaging!.ReadByte(stagingAddress);
        battle.Presentation!.NativeFileStaging!.Write(stagingAddress, [marker]);

        Assert.Null(live.State.Battle);
        Assert.True(live.HasActiveBattle);
        Assert.NotNull(live.LiveBattle);
        GameState snapshot = live.PrepareSnapshot();
        Assert.Equal(JsonSerializer.Serialize(validated.State.Battle), JsonSerializer.Serialize(snapshot.Battle));
        Assert.Equal(marker, snapshot.NativeBattleMemory!.FileStaging!.ReadByte(stagingAddress));
        Assert.Same(beforeReturn, live.State.NativeBattleMemory);
        Assert.Equal(prior, beforeReturn.FileStaging.ReadByte(stagingAddress));
        live.SetLiveBattle(null);
        Assert.Null(live.State.Battle);
        Assert.False(live.HasActiveBattle);
        Assert.Null(live.LiveBattle);
        Assert.NotNull(live.State.NativeBattleMemory);
        Assert.NotSame(beforeReturn, live.State.NativeBattleMemory);
        Assert.Equal(marker, live.State.NativeBattleMemory!.FileStaging!.ReadByte(stagingAddress));
    }

    [Fact]
    public void PublishedSnapshotRemainsIndependentOfLaterLiveCommands()
    {
        BattleSession battle = CreateBattle();
        GameSession live = GameSession.CreateColdProcess();
        live.SetLiveBattle(battle);
        BattleSnapshot published = live.PrepareSnapshot().Battle!;
        string before = JsonSerializer.Serialize(published);

        battle.SetMode(BattleAtbMode.TurnBased);

        Assert.Equal(before, JsonSerializer.Serialize(published));
        live.SetLiveBattle(battle);
        Assert.Equal(BattleOutcome.Active, published.Outcome);
        Assert.Null(live.State.Battle);
        BattleSnapshot current = live.PrepareSnapshot().Battle!;
        Assert.Equal(BattleAtbMode.TurnBased, current.Mode);
        Assert.Equal(BattleAtbMode.Normal, published.Mode);
        Assert.NotSame(published.Definition.Combatants, current.Definition.Combatants);
    }

    [Fact]
    public void SnapshotPublicationStillRejectsInvalidExternalState()
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSnapshot invalid = CreateBattle().Capture() with { NativePartyStatusMirrors = null };

        Assert.Throws<InvalidDataException>(() => session.SetBattle(invalid));
        Assert.Null(session.State.Battle);
    }

    [Fact]
    public void DirectSnapshotPreparationCapturesAttachedBattleAndKeepsPresenceGuardsLive()
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSession battle = CreateBattle();
        session.SetLiveBattle(battle);
        using IDisposable binding = session.BindSnapshotPreparation(state => state with { FieldName = "prepared" });

        battle.Tick();
        battle.SetMode(BattleAtbMode.TurnBased);
        GameState captured = session.PrepareSnapshot();

        Assert.Equal("prepared", captured.FieldName);
        Assert.Equal(battle.Frame, captured.Battle!.Frame);
        Assert.Equal(BattleAtbMode.TurnBased, captured.Battle.Mode);
        Assert.Null(session.State.Battle);
        Assert.True(session.HasActiveBattle);
        session.CompleteSnapshotPreparation(binding);
        Assert.Equal(battle.Frame, session.State.Battle!.Frame);
        Assert.True(session.HasActiveBattle);
    }

    [Fact]
    public void FaultAbandonmentDetachesAnUncapturableLiveBattleWithoutChangingPublishedState()
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSession battle = CreateBattle();
        session.SetLiveBattle(battle);
        GameState published = session.State;
        using IDisposable binding = session.BindSnapshotPreparation(state => state);
        using NativeBattleFileStagingTransaction transaction = new(NativeBattleFileStaging.Unknown());
        NativeBattleHostedTestFixture.SetProperty(battle.Presentation!, nameof(BattlePresentationProcess.NativeFileStaging), transaction.View);
        Assert.Throws<InvalidOperationException>(() => session.PrepareSnapshot());

        session.AbandonSnapshotPreparation(binding);
        session.CompleteSnapshotPreparation(binding);

        Assert.Null(session.LiveBattle);
        Assert.False(session.HasActiveBattle);
        Assert.Same(published, session.State);
        Assert.Equal(JsonSerializer.Serialize(published), JsonSerializer.Serialize(session.PrepareSnapshot()));
    }

    [Fact]
    public void Reading_definition_copies_catalog_metadata_without_capturing_live_effect_storage()
    {
        BattleSession battle = CreateBattle();
        NativeBattleEffectMemory memory = battle.Presentation!.Memory.BeginLoad(0);
        NativeBattleHostedTestFixture.SetProperty(battle.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), memory);
        long captures = memory.Arena!.Working.CaptureBytes;

        BattleDefinition first = battle.ReadDefinition();
        BattleDefinition second = battle.ReadDefinition();

        Assert.NotSame(first.Combatants, second.Combatants);
        Assert.NotSame(first.Combatants[0], second.Combatants[0]);
        Assert.Equal(first.Combatants[0].Id, second.Combatants[0].Id);
        Assert.Equal(captures, memory.Arena.Working.CaptureBytes);
        Assert.Null(first.NativeRetainedMemory?.EffectMemory?.Arena);
    }

    private static BattleSession CreateBattle()
    {
        BattleDefinition definition = new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            StageId = 0,
            NativeStageSource = new(0, [], [], new("stage", [])) { PhysicalSource = new(1, 0, []) },
            NativeStageCameraBank = new([]),
            NativeEntryWaveFile = new(0, "entry", []),
            NativeMusicProcess = new(new(1, null, null, null, 0, null), 0, null),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeGlobalRandom = new(123),
            Combatants =
            [
                new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, SceneSlot = 0, Information246 = 0 }
            ]
        };
        NativeBattleGraphicsResource model = new([0, 0])
        {
            AnimationFrames = [1],
            ModelLoadSources = [new(0, false, 0, 1, "body", [])]
        };
        NativeBattleGraphicsDefinition graphics = NativeBattleHostedTestFixture.CreateGraphicsDefinition(
            new("party", 0, model) { InitialPosition = new(0, 0, 0) },
            new("enemy", 16, model) { InitialPosition = new(0, 0, 0) });
        return NativeBattleTypedFixture.CreateBattle(definition, BattleAtbMode.Normal, NativeBattleHostedTestFixture.PrimitiveExecutionGraphics(graphics));
    }
}
