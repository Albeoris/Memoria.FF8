using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleProcessTransportTests
{
    [Fact]
    public void File_IO_rows_and_actual_vibration_owner_survive_snapshot_retained_capture_and_battle_return()
    {
        NativeBattleFileIoState io = NativeBattleFileIoState.FromColdProcess().Queue482610(40960, 0x508470, null).Advance482590((current, _) => current);
        NativeVibrationProcessState vibration = NativeVibrationProcessState.FromColdProcess() with { GraphicsSpeed20409F0 = 123, QuarterClockRemainder = 2 };
        BattleDefinition definition = Definition() with { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { FileIo = io }, NativeVibrationProcess = vibration };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        Assert.Same(io, battle.NativeFileIo);
        Assert.Same(vibration, battle.NativeVibrationProcess);
        BattleSnapshot saved = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(restored.Capture()));
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.Capture(restored.Capture(), null);
        Assert.Equal(JsonSerializer.Serialize(io), JsonSerializer.Serialize(memory.FileIo));
        Assert.Equal(JsonSerializer.Serialize(vibration), JsonSerializer.Serialize(memory.VibrationProcess));
        GameSession game = GameSession.CreateColdProcess();
        game.SetBattle(restored.Capture());
        Assert.Same(restored.NativeVibrationProcess, game.State.VibrationProcess);
        game.SetBattle(null);
        Assert.Equal(JsonSerializer.Serialize(io), JsonSerializer.Serialize(game.State.NativeBattleMemory!.FileIo));
        Assert.Equal(JsonSerializer.Serialize(vibration), JsonSerializer.Serialize(game.State.VibrationProcess));
        BattleSession next = new(Definition() with { NativeRetainedMemory = game.State.NativeBattleMemory }, BattleAtbMode.Normal);
        Assert.Equal(JsonSerializer.Serialize(vibration), JsonSerializer.Serialize(next.NativeVibrationProcess));
    }

    [Fact]
    public void Missing_old_snapshot_processes_remain_unknown_and_are_not_recreated_from_definition()
    {
        BattleSession fresh = new(Definition() with { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess() }, BattleAtbMode.Normal);
        BattleSnapshot missing = fresh.Capture() with { NativeFileIo = null, NativeVibrationProcess = null };
        BattleSession restored = BattleSession.Restore(missing);
        Assert.Null(restored.NativeFileIo);
        Assert.Null(restored.NativeVibrationProcess);
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.Capture(restored.Capture(), null);
        Assert.Null(memory.FileIo);
        Assert.Null(memory.VibrationProcess);
    }

    private static BattleDefinition Definition() => new()
    {
        Combatants =
        [
            new() { Id = "party", IsPlayer = true, Name = "Party" },
            new() { Id = "enemy", Name = "Enemy" }
        ]
    };
}
