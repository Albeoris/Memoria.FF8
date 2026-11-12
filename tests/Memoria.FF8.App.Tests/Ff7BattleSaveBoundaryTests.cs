using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7BattleSaveBoundaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(PlayerSaveStore.MemoriaSlotCount + 1)]
    public void Battle_disables_quick_memoria_and_original_player_saves(int slot)
    {
        Ff7Session session = (Ff7Session)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Session));
        typeof(Ff7Session).GetField("<Battle>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, RuntimeHelpers.GetUninitializedObject(typeof(Ff7BattleSession)));
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        typeof(Ff7Scene).GetField("<Session>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, session);

        Assert.False(session.CanSave);
        Assert.False(session.CanSaveMemoria);
        Assert.False(scene.CanSave);
        Assert.False(scene.CanSaveMemoria);
        Assert.Throws<InvalidOperationException>(() => scene.CapturePlayerSave(slot));
    }

    [Theory]
    [InlineData(Ff7BattleOutcome.Fighting)]
    [InlineData(Ff7BattleOutcome.Victory)]
    [InlineData(Ff7BattleOutcome.Defeat)]
    [InlineData(Ff7BattleOutcome.Escaped)]
    public void Durable_save_rejects_every_battle_phase_and_keeps_diagnostic_codec_available(Ff7BattleOutcome outcome)
    {
        CombinedSessionSave save = Fixture(outcome);
        using MemoryStream diagnostic = new();
        CombinedSessionCodec.Write(diagnostic, save);
        diagnostic.Position = 0;
        CombinedSessionSave restored = CombinedSessionCodec.Read(diagnostic);
        Assert.Equal(outcome, restored.Ff7!.Battle!.Outcome);
        Assert.Equal(73, restored.Ff7.Battle.Tick);
        Assert.Equal(save.Ff7!.Progress.Gil, restored.Ff7.Progress.Gil);
        AssertDurableSaveRejected(save);
    }

    [Fact]
    public void Durable_save_rejects_the_pending_battle_request_before_allocation()
    {
        CombinedSessionSave save = Fixture(Ff7BattleOutcome.Fighting);
        AssertDurableSaveRejected(save with { Ff7 = save.Ff7! with { Battle = null } });
    }

    private static void AssertDurableSaveRejected(CombinedSessionSave save)
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-ff7-battle-save-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "state.ffsave");
        try
        {
            Assert.Throws<InvalidOperationException>(() => CombinedSessionCodec.Save(path, save));
            Assert.False(Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            byte[] previous = [1, 2, 3];
            File.WriteAllBytes(path, previous);
            Assert.Throws<InvalidOperationException>(() => CombinedSessionCodec.Save(path, save));
            Assert.Equal(previous, File.ReadAllBytes(path));
            Assert.Single(Directory.EnumerateFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static CombinedSessionSave Fixture(Ff7BattleOutcome outcome)
    {
        FieldMachine machine = new(new FieldProgram([0, 0x5F, 0x12, 1], [new ushort[32]]));
        machine.Initialize();
        Ff7SessionSave ff7 = new()
        {
            ResourceProfile = "fixture",
            Language = "en",
            FieldName = "fixture",
            Progress = new() { InitialKernelData = [1], Characters = [new(0, "Cloud", 6, 100, 314, 54, new byte[132])], Party = [0, 255, 255], Inventory = [new(0, 2)], Materia = [], Gil = 240 },
            Vm = machine.Capture(),
            Process = new()
            {
                GlobalCommand = Ff7GlobalCommand.Battle,
                GlobalRequestId = 1,
                Receipts = [new(new(1, 0, 0, 0, new FieldBattleCommand(0)), new(false))]
            },
            Battle = new(1, 0, 0, 73, [], new(new byte[8]), Ff7BattleCommandQueueState.Create(), [], Outcome: outcome)
        };
        return new() { ActiveGame = SessionGame.Ff7, Ff7 = ff7 };
    }
}
