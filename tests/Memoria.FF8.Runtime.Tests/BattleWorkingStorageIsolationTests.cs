using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleWorkingStorageIsolationTests
{
    [Fact]
    public void Published_session_memory_keeps_its_physical_prefix_while_unpublished_task_banks_roll_back()
    {
        BattleSession session = CreateBattle(NativeBattleRetainedMemory.FromColdProcess());
        NativeBattleEffectMemory memory = session.Presentation!.Memory.BeginLoad(0);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x3000000, 0x20DFAB8, 20, 1).BindArena(memory.Arena!);
        NativeBattleEffectTaskPool auxiliary = NativeBattleEffectTaskPool.Initialize(0x3100000, 0x3200000, 20, 1);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleTaskOperation operation = new([main, auxiliary]);
            main.Slots[0].WriteDword(12, 17);
            auxiliary.Slots[0].WriteDword(12, 23);
            NativeBattleHostedTestFixture.SetProperty(session.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), memory);
            main.Slots[0].WriteDword(12, 99);
            auxiliary.Slots[0].WriteDword(12, 100);
            throw new InvalidOperationException("Unpublished suffix failed.");
        }));
        Assert.Equal(17u, session.Presentation!.Memory.Arena!.ReadUInt32(12));
        Assert.Equal(17u, main.Slots[0].ReadDword(12));
        Assert.Equal(0u, auxiliary.Slots[0].ReadDword(12));
        Assert.Equal(17u, session.CaptureForContinuation().ReadFixturePresentation().NativeEffectMemory!.Arena!.ReadUInt32(12));
    }

    [Fact]
    public void Captures_definitions_cold_templates_and_two_restores_have_independent_owned_banks()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        string coldJson = JsonSerializer.Serialize(cold);
        BattleSession live = CreateBattle(cold);
        NativeBattleQuake38State fields = live.Presentation!.Memory.Quake38!;
        fields.SetDword(0x278C8E0, 17);
        BattleSnapshot first = live.CaptureForContinuation();
        string firstJson = JsonSerializer.Serialize(first);
        fields.SetDword(0x278C8E0, 18);
        BattleSnapshot second = live.CaptureForContinuation();
        BattleSnapshot document = JsonSerializer.Deserialize<BattleSnapshot>(firstJson)!;
        BattleSession a = NativeBattleTypedFixture.RestoreBattle(document with
        {
            PresentationContinuation = first.PresentationContinuation,
            LogicalContinuation = first.LogicalContinuation
        });
        BattleSession b = NativeBattleTypedFixture.RestoreBattle(first);
        NativeBattleQuake38State restored = a.Presentation!.Memory.Quake38!;
        restored.SetDword(0x278C8E0, 19);

        Assert.Equal(19u, restored.Dword(0x278C8E0));
        Assert.Equal(17u, b.Presentation!.Memory.Quake38!.Dword(0x278C8E0));
        Assert.Equal(17u, first.ReadFixturePresentation().NativeEffectMemory!.Quake38!.Dword(0x278C8E0));
        Assert.Equal(18u, second.ReadFixturePresentation().NativeEffectMemory!.Quake38!.Dword(0x278C8E0));
        Assert.Equal(18u, fields.Dword(0x278C8E0));
        Assert.Equal(firstJson, JsonSerializer.Serialize(first));
        Assert.Equal(coldJson, JsonSerializer.Serialize(cold));
        Assert.Equal(0u, first.Definition.NativeRetainedMemory!.EffectMemory!.Quake38!.Dword(0x278C8E0));
        Assert.Throws<InvalidOperationException>(() => first.ReadFixturePresentation().NativeEffectMemory!.Quake38!.SetByte(0x278C8E0, 0));
        Assert.Throws<InvalidOperationException>(() => cold.EffectMemory!.Quake38!.SetByte(0x278C8E0, 0));
    }

    [Fact]
    public void A_checkpoint_cannot_escape_through_battle_capture()
    {
        BattleSession live = CreateBattle(NativeBattleRetainedMemory.FromColdProcess());
        NativeBattleQuake38State fields = live.Presentation!.Memory.Quake38!;
        fields.BeginCheckpoint();
        fields.SetDword(0x278C8E0, 20);
        Assert.Equal(0u, live.Capture().Definition.NativeRetainedMemory!.EffectMemory!.Quake38!.Dword(0x278C8E0));
        Assert.Throws<InvalidOperationException>(() => live.CaptureForContinuation());
        fields.RollbackCheckpoint();
        Assert.Equal(0u, live.CaptureForContinuation().ReadFixturePresentation().NativeEffectMemory!.Quake38!.Dword(0x278C8E0));
    }

    private static BattleSession CreateBattle(NativeBattleRetainedMemory memory)
    {
        BattleDefinition definition = new()
        {
            NativeRetainedMemory = memory,
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
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, Information246 = 0 }
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
