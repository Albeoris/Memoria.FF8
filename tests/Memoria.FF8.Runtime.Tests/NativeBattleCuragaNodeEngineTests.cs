using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaNodeEngineTests
{
    private const int First = 0xC000;
    private const int Second = First + 0x25F8;
    private const uint Base = NativeBattleCuragaExecutableState.Address;

    [Fact]
    public void Generic_task_waits_on_its_signed_WORD_delay_and_wraps_its_counter_without_initializing_early()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = Write(fixture.Frame, fixture.Task, task => task.WriteWord(0x24, 65534).WriteWord(0x298, 32767));
        NativeBattleCuragaLeafStep result = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(frame, fixture.Task, new(new("unused", []), 0), NoControllerInputs(), NoLeaves());
        Assert.Equal(0, result.Result);
        Assert.Equal((ushort)65535, Task(result.Frame, fixture.Task).ReadWord(0x24));
        Assert.Equal((byte)0, Task(result.Frame, fixture.Task).ReadByte(0x29));
        Assert.Equal((ushort)0, Task(result.Frame, fixture.Task).ReadWord(0x34 + 0x20));
    }

    [Fact]
    public void Completed_engine_sets_generic_task_flag_then_releases_only_after_its_actual_child_count_is_zero()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = Write(fixture.Frame, fixture.Task, task => task.WriteByte(0x29, 1).WriteByte(0x28, 1).WriteWord(0x34 + 0x20, 2));
        NativeBattleCuragaLeafStep result = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(frame, fixture.Task, new(new("unused", []), 0), NoControllerInputs(), NoLeaves());
        Assert.Equal(0, result.Result);
        Assert.Equal((byte)2, Task(result.Frame, fixture.Task).ReadByte(0x29));
        Assert.Equal((byte)1, Task(result.Frame, fixture.Task).ReadByte(0x26));
        frame = Write(result.Frame, fixture.Task, task => task.WriteByte(0x28, 0));
        result = NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(frame, fixture.Task, new(new("unused", []), 0), NoControllerInputs(), NoLeaves());
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)2, Task(result.Frame, fixture.Task).ReadWord(0x24));
    }

    [Fact]
    public void Zero_phase_only_advances_context_phase_and_accumulates_the_actual_WORD_counts()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = Write(fixture.Frame, fixture.Task, task => task.WriteWord(0x34 + 0x14, 2).WriteWord(0x34 + 0x16, 3));
        frame = frame with { State = frame.State with { PassCount26D82EA = 65535, PassCount26D82DA = 65534 } };
        NativeBattleCuragaNodeEngineStep result = NativeBattleCuragaNodeEngine.Advance87EC80(frame, fixture.Task, NoLeaves());
        Assert.False(result.Complete);
        Assert.Equal((ushort)1, Task(result.Frame, fixture.Task).ReadWord(0x34 + 0x20));
        Assert.Equal((ushort)0, Task(result.Frame, fixture.Task).ReadWord(0x34 + 0x18));
        Assert.Equal((ushort)1, result.Frame.State.PassCount26D82EA);
        Assert.Equal((ushort)1, result.Frame.State.PassCount26D82DA);
    }

    [Fact]
    public void Birth_scan_uses_modes_mask_signed_birth_frame_and_signed_target_count()
    {
        var fixture = Fixture([(0, 0, true), (1, -1, false), (4, 0, false), (3, 0, false), (0, 0, false), (1, 0, true)]);
        NativeBattleCuragaFrame frame = Write(fixture.Frame, fixture.Task, task => task.WriteWord(0x34 + 0x1C, 2).WriteWord(0x34 + 0x22, 1 << 4));
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeEngine.Birth882E20(frame, fixture.Task);
        Assert.Equal((ushort)5, Task(result, fixture.Task).ReadWord(0x34 + 0x14));
        Assert.Equal(new byte[] { 0, 0 }, result.Memory.Arena!.Read(First + 0x6A, 2));
        Assert.Equal(new byte[] { 0, 1 }, result.Memory.Arena.Read(First + 108 + 0x6A, 2));
        Assert.Equal(new byte[] { 2, 0 }, result.Memory.Arena.Read(First + 216 + 0x6A, 2));
        Assert.Equal(new byte[] { 5, 0 }, result.Memory.Arena.Read(First + 324 + 0x6A, 2));
        Assert.Equal(new byte[] { 5, 1 }, result.Memory.Arena.Read(First + 432 + 0x6A, 2));
        frame = Write(frame, fixture.Task, task => task.WriteWord(0x34 + 0x18, 65535).WriteWord(0x34 + 0x1C, 65535));
        result = NativeBattleCuragaNodeEngine.Birth882E20(frame, fixture.Task);
        Assert.Equal((ushort)1, Task(result, fixture.Task).ReadWord(0x34 + 0x14));
        Assert.Equal(new byte[] { 1, 0 }, result.Memory.Arena!.Read(First + 0x6A, 2));
    }

    [Fact]
    public void List_walk_reads_next_after_the_callback_so_appended_geometry_runs_after_removed_emitter_in_the_same_pass()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaNodes.AllocateEmitter881700(fixture.Frame, fixture.Task, 0, 5, 0).Frame;
        NativeBattleCuragaFrame clone = JsonSerializer.Deserialize<NativeBattleCuragaFrame>(JsonSerializer.Serialize(frame))!;
        List<string> calls = [];
        NativeBattleCuragaFrame actual = NativeBattleCuragaNodeEngine.AdvanceList8804D0(frame, fixture.Task, Emitter, Geometry);
        Assert.Equal(new[] { "emitter", "geometry" }, calls);
        Assert.Equal(0u, Task(actual, fixture.Task).ReadDword(0x34 + 0x2C));
        Assert.Equal((ushort)0, Task(actual, fixture.Task).ReadWord(0x34 + 0x14));
        Assert.Equal((ushort)0, Task(actual, fixture.Task).ReadWord(0x34 + 0x16));
        calls.Clear();
        NativeBattleCuragaFrame restored = NativeBattleCuragaNodeEngine.AdvanceList8804D0(clone, fixture.Task, Emitter, Geometry);
        Assert.Equal(new[] { "emitter", "geometry" }, calls);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));

        NativeBattleCuragaFrame Emitter(NativeBattleCuragaFrame current, uint context, int node)
        {
            calls.Add("emitter");
            current = NativeBattleCuragaNodes.AllocateGeometry882080(current, context, node, 5).Frame;
            return NativeBattleCuragaNodes.AdvanceEmitterLifetime8817F0(current, context, node);
        }
        NativeBattleCuragaFrame Geometry(NativeBattleCuragaFrame current, uint context, int node)
        {
            calls.Add("geometry");
            Assert.Equal(Second, node);
            current = NativeBattleCuragaNodes.Unlink882960(current, context, node);
            return Write(current, context, task => task.WriteWord(0x34 + 0x16, unchecked((ushort)(task.ReadWord(0x34 + 0x16) - 1))));
        }
    }

    [Fact]
    public void Active_phase_invokes_one_combined_callback_and_only_reports_completion_on_the_next_callback()
    {
        var fixture = Fixture([(0, 0, false)]);
        NativeBattleCuragaFrame frame = Write(fixture.Frame, fixture.Task, task => task.WriteWord(0x34 + 0x20, 1).WriteWord(0x34 + 0x1A, 1));
        List<string> calls = [];
        NativeBattleCuragaNodeEngineInputs inputs = new((current, context) =>
        {
            calls.Add("combined");
            current = NativeBattleCuragaNodeEngine.Birth882E20(current, context);
            return NativeBattleCuragaNodeEngine.AdvanceList8804D0(current, context, (value, taskAddress, node) =>
            {
                calls.Add("update");
                Assert.Equal((ushort)1, Task(value, taskAddress).ReadWord(0x34 + 0x14));
                return NativeBattleCuragaNodes.AdvanceEmitterLifetime8817F0(value, taskAddress, node);
            }, (_, _, _) => throw new InvalidOperationException());
        });
        NativeBattleCuragaNodeEngineStep result = NativeBattleCuragaNodeEngine.Advance87EC80(frame, fixture.Task, inputs);
        Assert.Equal(new[] { "combined", "update" }, calls);
        Assert.False(result.Complete);
        Assert.Equal((ushort)2, Task(result.Frame, fixture.Task).ReadWord(0x34 + 0x20));
        Assert.Equal((ushort)1, Task(result.Frame, fixture.Task).ReadWord(0x34 + 0x18));
        Assert.True(NativeBattleCuragaNodeEngine.Advance87EC80(result.Frame, fixture.Task, NoLeaves()).Complete);
    }

    [Fact]
    public void Unknown_pass_count_owner_is_not_replaced_by_a_fabricated_zero()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = fixture.Frame with { State = fixture.Frame.State with { PassCount26D82DA = null } };
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaNodeEngine.Advance87EC80(frame, fixture.Task, NoLeaves()));
    }

    private static NativeBattleCuragaNodeEngineInputs NoLeaves() => new((_, _) => throw new InvalidOperationException());
    private static NativeBattleCuragaControllerInputs NoControllerInputs() => new(NativeBattleActionPacketsState.Fresh(), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
    private static NativeBattleModulePoolTask Task(NativeBattleCuragaFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleCuragaFrame Write(NativeBattleCuragaFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, update(hierarchy.Read(address))).Pools } };
    }

    internal static (NativeBattleCuragaFrame Frame, uint Task) Fixture((byte Mode, sbyte Birth, bool PerTarget)[]? sources = null)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 23, 28, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(28, 27, new(28, 0x87DB10, 0x87DAF0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x26D75BC, 0)] };
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(NativeBattleEffectMemory.FromColdProcess().BeginLoad(27), selected, packets, new(new("skipped-registration", []), 0x4946F));
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(3, 0x87E1A0, 676, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        byte[] bytes = new byte[NativeBattleCuragaExecutableState.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        sources ??= [];
        for (int index = 0; index < sources.Length; index++)
        {
            int offset = 0x104 + index * 0x158;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + index * 4), Base + (uint)offset);
            bytes[offset + 0x11] = sources[index].Mode;
            bytes[offset + 0x13] = unchecked((byte)sources[index].Birth);
            bytes[offset + 0x37] = sources[index].PerTarget ? (byte)1 : (byte)0;
        }
        frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Pools }, Memory = frame.Memory with { CuragaExecutable = new(1, bytes.ToImmutableArray()) }, State = frame.State with { Auxiliary25F8 = First, Auxiliary16F80 = Second, Counter26D76C0 = 0, Counter26D82E8 = 0, PassCount26D82EA = 0, PassCount26D82DA = 0 } };
        frame = Write(frame, child.Address, task => task.WriteDword(0x34 + 0x224, Base + 4));
        return (frame, child.Address);
    }
}
