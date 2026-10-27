using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedCommandsTests
{
    private const uint Manager = 0x2797690;
    private const uint Scene = NativeBattleScriptedEffectMemory.Address;
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 8, 0x1000);

    [Fact]
    public void Process_flags_mutate_all_four_rows_preserving_bits_and_restore_independently()
    {
        NativeBattleStageState cold = NativeBattleStageState.FromColdProcess();
        byte[] flags = [0x80, 1, 0xFF, 4];
        NativeBattleStageState initial = cold with { Groups = cold.Groups.Select((group, index) => group with { Flags = flags[index], ColorDepth = 123, ColorFlags = 0xAABBCCDD }).ToImmutableArray() };
        NativeBattleScriptedProcessFlagsStep set = NativeBattleScriptedProcessFlags.Execute(Fixture(0x6D), initial);
        Assert.Equal(new byte[] { 0x82, 3, 0xFF, 6 }, set.State.Groups.Select(group => group.Flags));
        NativeBattleStageState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleStageState>(NativeBattleTestSnapshots.Json(set.State))!;
        NativeBattleScriptedProcessFlagsStep clear = NativeBattleScriptedProcessFlags.Execute(set.Frame.WriteTemporaryWord(0x14A, 0x806D), restored);
        Assert.Equal(new byte[] { 0x80, 1, 0xFD, 4 }, clear.State.Groups.Select(group => group.Flags));
        Assert.All(clear.State.Groups, group =>
        {
            Assert.Equal((short)123, group.ColorDepth);
            Assert.Equal(0xAABBCCDDu, group.ColorFlags);
        });
        Assert.Equal(0x1004u, Pc(clear.Frame));
        Assert.Equal((ushort)0x1234, clear.Frame.ReadTemporaryWord(0x13E));
        Assert.Throws<InvalidDataException>(() => NativeBattleScriptedProcessFlags.Execute(Fixture(0x6D), cold with { Groups = [] }));
    }

    [Theory]
    [InlineData(1, true, 0, 3, true)]
    [InlineData(1, false, 3, 4, true)]
    [InlineData(2, true, 3, 4, false)]
    [InlineData(3, false, 0, 3, true)]
    [InlineData(4, false, 0, 7, false)]
    [InlineData(5, true, 0, 7, true)]
    [InlineData(-1, true, 0, 3, false)]
    [InlineData(6, false, 3, 4, false)]
    public void Visibility_selects_root_side_and_exact_operand_rows(short operand, bool party, int first, int count, bool hide)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x13D, operand);
        frame = frame with { Arena = frame.Arena.Write(0x201B, [party ? (byte)2 : (byte)3]).Write(0x211B, [party ? (byte)6 : (byte)0]) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973E8, Address(0x2100));
        ushort[] flags = Enumerable.Repeat((ushort)0xA107, 7).ToArray();
        List<int> writes = [];
        NativeBattleScriptedVisibilityServices services = new((_, row) => flags[row], (current, row, value) =>
        {
            writes.Add(row);
            flags[row] = value;
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedVisibility.Execute(frame, Program, services);
        Assert.Equal(Enumerable.Range(first, count), writes);
        foreach (int row in writes)
        {
            Assert.Equal(hide ? (ushort)0xA107 : (ushort)0xA103, flags[row]);
        }
        Assert.Equal(0x1004u, Pc(result));
    }

    [Fact]
    public void Restore_preserves_original_hidden_row_and_rereads_captured_scene_after_each_service()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x13D, 4);
        frame = WaterSchedulerFixture.CoreWord(frame, Scene + 0x4E, 0xFF04);
        List<int> writes = [];
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedVisibility.Execute(frame, Program, new((_, _) => 0xFFEF, (current, row, flags) =>
        {
            writes.Add(row);
            if (row == 1)
            {
                current = WaterSchedulerFixture.CoreWord(current, Scene + 0x4E + 4, 4);
            }
            return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFC);
        }));
        Assert.Equal(new[] { 1, 3, 4, 5, 6 }, writes);
        Assert.Equal(0u, Pc(result));
    }

    [Fact]
    public void Stream_uses_selected_actual_directory_and_synchronously_publishes_ready()
    {
        NativeBattleWaterExecutionFrame frame = StreamFixture(0x8031);
        int clears = 0;
        NativeBattleScriptedStreamStep result = NativeBattleScriptedStreamCommands.Execute(frame, NativeBattleSummonStreamState.Unknown(), UnexpectedRead, current =>
        {
            clears++;
            return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFE);
        });
        Assert.Equal(1, clears);
        Assert.Equal(Address(0x5120), result.Stream.SourceAddress);
        Assert.Equal(-1, result.Stream.Number);
        Assert.Equal((byte)1, result.Frame.Core.Read(Manager + 0xA3, 1)[0]);
        Assert.Equal(0u, Pc(result.Frame));
        NativeBattleScriptedStreamStep ready = NativeBattleScriptedStreamCommands.Execute(result.Frame.WriteTemporaryWord(0x14A, 0x34), result.Stream, UnexpectedRead, UnexpectedClear);
        Assert.Equal(2u, Pc(ready.Frame));
        Assert.Equal((ushort)0x1234, ready.Frame.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void Stream_busy_and_ready_waits_do_not_consume_unreached_resource_fields()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x31);
        frame = frame with { Core = frame.Core.Write(0x2798219, [1]).MarkUnknown(Manager + 0xB0, 12) };
        NativeBattleSummonStreamState stream = NativeBattleSummonStreamState.Unknown();
        NativeBattleScriptedStreamStep busy = NativeBattleScriptedStreamCommands.Execute(frame, stream, UnexpectedRead, UnexpectedClear);
        Assert.Equal(0x1000u, Pc(busy.Frame));
        Assert.Equal((ushort)3, busy.Frame.ReadTemporaryWord(0x13E));
        NativeBattleScriptedStreamStep wait = NativeBattleScriptedStreamCommands.Execute(frame.WriteTemporaryWord(0x14A, 0x34), stream, UnexpectedRead, UnexpectedClear);
        Assert.Equal(0x1000u, Pc(wait.Frame));
        Assert.Equal((ushort)3, wait.Frame.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void Stream_map_FF_uses_current_B8_and_external_map_reads_require_exact_extent()
    {
        NativeBattleWaterExecutionFrame frame = StreamFixture(0x1E31);
        frame = frame with { Arena = frame.Arena.Write(0x400F, [0xFF]) };
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xB8, Address(0x5120));
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleScriptedStreamStep result = NativeBattleScriptedStreamCommands.Execute(frame, NativeBattleSummonStreamState.Unknown(), UnexpectedRead, UnexpectedClear);
        Assert.Equal(-1, result.Stream.Number);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleScriptedStreamCommands.Execute(restored, NativeBattleSummonStreamState.Unknown(), UnexpectedRead, UnexpectedClear)));
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xB0, 0x30000000);
        Assert.Throws<InvalidDataException>(() => NativeBattleScriptedStreamCommands.Execute(frame, NativeBattleSummonStreamState.Unknown(), (_, _, _) => [], UnexpectedClear));
    }

    [Fact]
    public void Vibration_uses_current_file_directory_and_real_handle_then_stops_three_handles_without_clearing_them()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x2039, 0);
        frame = frame with { Arena = frame.Arena.Write(0x3000 + 0x24, BitConverter.GetBytes(0x800u)) };
        NativeBattleScriptedVibrationStep result = NativeBattleScriptedVibrationCommands.Execute(frame, Program, NativeVibrationProcessState.FromColdProcess(), (_, address) =>
        {
            Assert.Equal(Address(0x3800), address);
            return Bank();
        });
        int handle = unchecked((int)WaterSchedulerFixture.ReadCoreDword(result.Frame, Scene + 0x168));
        Assert.Equal(32, handle);
        Assert.True(result.Vibration.IsPlaying(handle));
        NativeBattleWaterExecutionFrame stopFrame = result.Frame.WriteTemporaryWord(0x14A, 0x3039);
        stopFrame = WaterSchedulerFixture.CoreDword(stopFrame, Scene + 0x16C, uint.MaxValue);
        NativeBattleScriptedVibrationStep stopped = NativeBattleScriptedVibrationCommands.Execute(stopFrame, Program, result.Vibration, (_, _) => throw new InvalidOperationException());
        Assert.False(stopped.Vibration.IsPlaying(handle));
        Assert.Equal((uint)handle, WaterSchedulerFixture.ReadCoreDword(stopped.Frame, Scene + 0x168));
        Assert.Equal(uint.MaxValue, WaterSchedulerFixture.ReadCoreDword(stopped.Frame, Scene + 0x16C));
        Assert.Equal(0x1008u, Pc(stopped.Frame));
    }

    [Fact]
    public void Vibration_unsigned_operand_bytes_preserve_pattern256_and_handle_minus_one()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x2039);
        frame = frame with { Arena = frame.Arena.Write(0x1002, [0xFF, 0xFF]) };
        byte[] bytes = new byte[257 * 4];
        NativeVibrationBank bank = new(new string('0', 64), Enumerable.Repeat(0, 257).ToImmutableArray(), bytes.ToImmutableArray());
        NativeBattleScriptedVibrationStep result = NativeBattleScriptedVibrationCommands.Execute(frame, Program, NativeVibrationProcessState.FromColdProcess(), (_, _) => bank);
        Assert.Equal(uint.MaxValue, WaterSchedulerFixture.ReadCoreDword(result.Frame, Scene + 0x168 + 4 * 255));
    }

    [Theory]
    [InlineData(0x1039, "animation")]
    [InlineData(0x5039, "enabled1")]
    [InlineData(0x6039, "enabled1")]
    [InlineData(0x7039, "enabled0")]
    [InlineData(0x8039, "restore")]
    public void Ifrit_camera_modes_dispatch_exact_services_and_use_live_PC(ushort instruction, string expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = frame with { Arena = frame.Arena.Write(0x3020, BitConverter.GetBytes(0x880u)) };
        List<string> calls = [];
        NativeBattleWaterExecutionFrame Finish(NativeBattleWaterExecutionFrame current, string call)
        {
            calls.Add(call);
            return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFC);
        }
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedCameraCommands.Execute(frame, new((current, address) =>
        {
            Assert.Equal(Address(0x3880), address);
            return Finish(current, "animation");
        }, current => Finish(current, "restore"))
        {
            ReadGraphicsFlags = _ => 0,
            PublishGraphicsFlags = (current, flags) => Finish(current, "enabled" + ((flags & 0x100) != 0 ? 1 : 0))
        });
        Assert.Equal(new[] { expected }, calls);
        Assert.Equal(0u, Pc(result));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, short operand = 0)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction).WriteTemporaryWord(0x13E, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A68, Address(0x3000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279744C, Scene);
        return frame with { Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(operand)) };
    }

    private static NativeBattleWaterExecutionFrame StreamFixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xB0, Address(0x4000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, Address(0x5000));
        return frame with { Arena = frame.Arena.Write(0x4000, [1]).Write(0x5018, BitConverter.GetBytes(0x100u)).Write(0x5100, BitConverter.GetBytes(0x20u)).Write(0x5120, BitConverter.GetBytes(-690001)) };
    }

    private static NativeVibrationBank Bank() => new(new string('0', 64), [0, 8], new byte[] { 0, 0, 0, 0, 8, 0, 0, 0, 2, 0, 0, 0, 1, 255, 0, 0 }.ToImmutableArray());
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
    private static byte[] UnexpectedRead(NativeBattleWaterExecutionFrame frame, uint address, int length) => throw new InvalidOperationException("Unexpected external read.");
    private static NativeBattleWaterExecutionFrame UnexpectedClear(NativeBattleWaterExecutionFrame frame) => throw new InvalidOperationException("Unexpected external flag clear.");
}
