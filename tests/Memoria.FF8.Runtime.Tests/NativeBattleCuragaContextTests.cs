using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaContextTests
{
    [Fact]
    public void Initial_context_uses_actual_model_WORD36_bounds_WORD60_and_Q16_anchors_without_consuming_random_state()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame initialized = NativeBattleCuragaContext.Initialize87E230(fixture.Frame, fixture.Address, Resources(), fixture.Inputs);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(initialized.Effect.Pools).Read(fixture.Address);
        Assert.Equal((ushort)2, task.ReadWord(0x34 + 0x1C));
        Assert.Equal((ushort)2, task.ReadWord(0x34 + 0x1E));
        Assert.Equal((ushort)45, task.ReadWord(0x34 + 0x1A));
        Assert.Equal(3u, task.ReadDword(0x34 + 4));
        Assert.Equal(4u, task.ReadDword(0x34 + 8));
        AssertVector(task, 0x1C4, 0, -3, 0);
        AssertVector(task, 0x1E4, 0, 100, 0);
        AssertVector(task, 0x204, 0, 800, 0);
        AssertVector(task, 0x54, -10, 10, 30);
        AssertVector(task, 0xD4, -10, -200, 30);
        AssertVector(task, 0x154, -10, 900, 30);
        AssertVector(task, 0x194, 5, 0, -10);
        Assert.Equal(301u, task.ReadDword(0x34));
        Assert.Equal(NativeBattleCuragaExecutableState.Address + 4, task.ReadDword(0x34 + 0x224));
        Assert.Equal(1u, initialized.Memory.CuragaExecutable!.InitializationFlag);
        Assert.Equal(0u, fixture.Frame.Memory.CuragaExecutable!.InitializationFlag);
        NativeBattleCuragaFrame independent = JsonSerializer.Deserialize<NativeBattleCuragaFrame>(JsonSerializer.Serialize(fixture.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(initialized), JsonSerializer.Serialize(NativeBattleCuragaContext.Initialize87E230(independent, fixture.Address, Resources(), fixture.Inputs)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void Original_context_mode_selectors_copy_their_actual_four_DWORD_vectors(ushort mode)
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address).WriteWord(0x29C, mode).WriteWord(0x288, 100).WriteWord(0x28A, unchecked((ushort)-200)).WriteWord(0x28C, 300).WriteWord(0x290, 7).WriteWord(0x292, 8).WriteWord(0x294, 9);
        hierarchy = hierarchy.Write(fixture.Address, task);
        NativeBattleCuragaFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleCuragaContext.Initialize87E230(frame, fixture.Address, Resources(), fixture.Inputs);
        task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address);
        if (mode == 4)
        {
            foreach (int offset in new[] { 0x1C4, 0x1D4, 0x1E4, 0x1F4, 0x204 })
            {
                AssertVector(task, offset, 7, 8, 9);
            }
            AssertVector(task, 0x54, -10, 10, 30);
        }
        else
        {
            foreach (int offset in new[] { 0x54, 0x94, 0xD4, 0x114, 0x154 })
            {
                AssertVector(task, offset, mode == 1 ? 5 : 100, mode == 1 ? 0 : -200, mode == 1 ? -10 : 300);
                AssertVector(task, offset + 16, mode == 1 ? 5 : 100, mode == 1 ? 0 : -200, mode == 1 ? -10 : 300);
            }
        }
    }

    [Fact]
    public void Stack_VECTOR_mode_is_unknown_and_a_missing_retained_executable_owner_is_not_seeded()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x29C, 2));
        NativeBattleCuragaFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaContext.Initialize87E230(frame, fixture.Address, Resources(), fixture.Inputs));
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaContext.Initialize87E230(fixture.Frame with { Memory = fixture.Frame.Memory with { CuragaExecutable = null } }, fixture.Address, Resources(), fixture.Inputs));
    }

    private static void AssertVector(NativeBattleModulePoolTask task, int offset, int x, int y, int z)
    {
        Assert.Equal(unchecked((uint)(x << 16)), task.ReadDword(0x34 + offset));
        Assert.Equal(unchecked((uint)(y << 16)), task.ReadDword(0x34 + offset + 4));
        Assert.Equal(unchecked((uint)(z << 16)), task.ReadDword(0x34 + offset + 8));
    }

    private static NativeBattleCuragaResources Resources() => new(new("skipped-registration", []), 0x4946F) { Executable = new(new byte[NativeBattleCuragaExecutableState.Length].ToImmutableArray()) };

    private static (NativeBattleCuragaFrame Frame, uint Address, NativeBattleCuragaControllerInputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 23, 28, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(2);
        NativeBattleOriginalEffectState selected = new(28, 27, new(28, 0x87DB10, 0x87DAF0), new(2, 1, 24, 0, 0, 0), null) { ResourcePointers = [new(0x26D75BC, 0)] };
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(NativeBattleEffectMemory.FromColdProcess().BeginLoad(27), selected, packets, Resources());
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend first = hierarchy.Append8DC540(1, 0x87DF70, 88, hierarchy.Address(0, 0), (_, _, _) => new(2, 3));
        NativeBattleEffectHierarchyAppend child = first.Hierarchy.Append8DC540(3, 0x87E1A0, 676, first.Address, (_, _, _) => new(2, 3));
        hierarchy = child.Hierarchy;
        hierarchy = hierarchy.Write(child.Address, hierarchy.Read(child.Address).WriteDword(0x30, NativeBattleCuragaExecutableState.Address).WriteWord(0x29E, 45));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleGraphicsModel Model(int index)
        {
            NativeBattleVector position = index == 2 ? new(0, 100, 0) : index == 3 ? new(-10, -200, 30) : new(20, -400, -50);
            short angle36 = index == 2 ? (short)-3 : index == 3 ? (short)10 : (short)-20;
            return new(index, 0) { Transform = new(new(0, 0, 0), position, 0, angle36, 4096), GeometryBounds52 = new(new(-100, -200, -300), new((short)(600 + index * 100), 200, 300)) };
        }
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        return (frame, child.Address, new(packets, camera, Model, _ => source));
    }
}
