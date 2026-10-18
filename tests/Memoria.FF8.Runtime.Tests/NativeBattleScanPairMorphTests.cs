using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanPairMorphTests
{
    [Fact]
    public void Pair_delay_keeps_draw_disabled_until_counter_four_then_uses_signed_tables_and_actual_resource_order()
    {
        var fixture = Fixture(0x84D810);
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanPairMorphInputs inputs = Inputs(draws);
        NativeBattleScanPairMorphFrame frame = fixture.Frame;
        for (int index = 0; index < 4; index++)
        {
            frame = NativeBattleScanPairMorph.Advance84D810(frame, fixture.Address, inputs).Frame;
        }
        Assert.Empty(draws);
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        frame = NativeBattleScanPairMorph.Advance84D810(frame, fixture.Address, inputs).Frame;
        Assert.Equal(new uint[] { 0x15CE93C, 0x15CE9B8 }, draws.Select(draw => draw.ResourceAddress));
        Assert.All(draws, draw =>
        {
            Assert.Equal(-50, draw.Matrix.X);
            Assert.Equal(80, draw.Matrix.Y);
            Assert.Equal(0x480, draw.Matrix.Z);
        });
        Assert.Equal(new short[] { -1, 2048 }, draws.Select(draw => draw.Fade));
        Assert.Equal(0xFFFFF000u, Task(frame, fixture.Address).ReadDword(0x30));
        Assert.Equal(0x15CE9B8u, Task(frame, fixture.Address).ReadDword(0x4C));
        Assert.Equal((ushort)2048, Task(frame, fixture.Address).ReadWord(0x50));
    }

    [Fact]
    public void Pair_terminal_signal_still_draws_both_meshes_before_parent_completion()
    {
        var fixture = Fixture(0x84D810);
        NativeBattleScanPairMorphFrame frame = Change(fixture.Frame, fixture.Address, task => task.WriteByte(0x29, 3).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096));
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanPairMorphStep step = NativeBattleScanPairMorph.Advance84D810(frame with { Highlight269AA90 = 0 }, fixture.Address, Inputs(draws));
        Assert.Equal(2, step.Result);
        Assert.Equal(2, draws.Count);
        Assert.Equal((byte)4, Task(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((byte)0, Task(step.Frame, 0x269AAE0).ReadByte(0x28));
        Assert.Equal((ushort)1, Task(step.Frame, fixture.Address).Flags);
    }

    [Fact]
    public void Morph_initialization_and_shape_switch_write_only_XYZ_preserve_W_knownness_and_use_new_shape_same_callback()
    {
        var fixture = Fixture(0x84DAA0);
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanPairMorphInputs inputs = Inputs(draws);
        NativeBattleScanPairMorphFrame frame = NativeBattleScanPairMorph.Advance84DAA0(fixture.Frame, fixture.Address, inputs).Frame;
        Assert.Equal((ushort)100, frame.MutableMeshes[0x15CDDEC].ReadWord(8));
        Assert.False(frame.MutableMeshes[0x15CDDEC].Known[14]);
        Assert.Equal((byte)0xA5, frame.MutableMeshes[0x15CDDEC].Bytes[14]);
        NativeBattleScanByteOwner retained = frame.MutableMeshes[0x15CDDEC];
        frame = Change(frame, fixture.Address, task => task.WriteByte(0x29, 3).WriteWord(0x6A, 0xE00));
        frame = NativeBattleScanPairMorph.Advance84DAA0(frame, fixture.Address, inputs).Frame;
        Assert.Equal((byte)4, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)0, Task(frame, fixture.Address).ReadWord(0x6A));
        Assert.Equal((ushort)300, frame.MutableMeshes[0x15CE1D4].ReadWord(8));
        Assert.Same(retained, frame.MutableMeshes[0x15CDDEC]);
        Assert.False(frame.MutableMeshes[0x15CE1D4].Known[14]);
        Assert.Equal(0x15CE1D4u, draws[^1].ResourceAddress);
    }

    [Fact]
    public void Morph_timeline_clamps_scale_waits_twelve_ticks_and_reaches_actual_second_shape()
    {
        var fixture = Fixture(0x84DAA0);
        List<NativeBattleScanMeshDraw> draws = [];
        NativeBattleScanPairMorphInputs inputs = Inputs(draws);
        NativeBattleScanPairMorphFrame frame = NativeBattleScanPairMorph.Advance84DAA0(fixture.Frame, fixture.Address, inputs).Frame;
        for (int index = 0; index < 12; index++)
        {
            frame = NativeBattleScanPairMorph.Advance84DAA0(frame, fixture.Address, inputs).Frame;
        }
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(2048u, Task(frame, fixture.Address).ReadDword(0x30));
        Assert.Equal((ushort)12, Task(frame, fixture.Address).ReadWord(0x66));
        for (int index = 0; index < 24; index++)
        {
            frame = NativeBattleScanPairMorph.Advance84DAA0(frame, fixture.Address, inputs).Frame;
        }
        Assert.Equal((byte)5, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)4096, Task(frame, fixture.Address).ReadWord(0x6A));
        Assert.Equal((ushort)400, frame.MutableMeshes[0x15CE1D4].ReadWord(8));
        NativeBattleScanPairMorphStep complete = NativeBattleScanPairMorph.Advance84DAA0(frame with { Highlight269AA90 = 0 }, fixture.Address, inputs);
        Assert.Equal(2, complete.Result);
        Assert.Equal(38, draws.Count);
    }

    private static NativeBattleScanPairMorphInputs Inputs(List<NativeBattleScanMeshDraw> draws)
    {
        ImmutableDictionary<uint, ImmutableArray<byte>> tables = new Dictionary<uint, ImmutableArray<byte>>
        {
            [0x15D15A0] = Words(Enumerable.Repeat((short)-1, 18)),
            [0x15D15C4] = Words(Enumerable.Repeat((short)2048, 18)),
            [0x15D15E8] = Words(Enumerable.Range(0, 18).SelectMany(_ => new short[] { 10, 20 })),
            [0x15D1630] = Words(Enumerable.Range(0, 18).SelectMany(_ => new short[] { -4096, 2048 }))
        }.ToImmutableDictionary();
        NativeBattleScanVisualResources resources = new([])
        {
            Sound15D0B3C = 0,
            TablesByAddress = tables,
            MeshesByAddress = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty,
            VertexBanksByAddress = new Dictionary<uint, ImmutableArray<byte>> { [0x15CFAB4] = Bank(100), [0x15CFC3C] = Bank(200), [0x15CFDC4] = Bank(300), [0x15D02CC] = Bank(400) }.ToImmutableDictionary(),
            LineArrays15D1718 = [],
            Sprite15D07D4 = null!,
            Sprite15D0AF8 = null!
        };
        return new(resources, (frame, draw) =>
        {
            draws.Add(draw);
            return frame;
        });
    }

    private static ImmutableArray<byte> Words(IEnumerable<short> words)
    {
        short[] values = words.ToArray();
        byte[] bytes = new byte[values.Length * 2];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), values[index]);
        }
        return bytes.ToImmutableArray();
    }

    private static ImmutableArray<byte> Bank(short x)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        return bytes.ToImmutableArray();
    }

    private static NativeBattleModulePoolTask Task(NativeBattleScanPairMorphFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);

    private static NativeBattleScanPairMorphFrame Change(NativeBattleScanPairMorphFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        hierarchy = hierarchy.Write(address, change(hierarchy.Read(address)));
        return frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
    }

    private static (NativeBattleScanPairMorphFrame Frame, uint Address) Fixture(uint callback)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(scan.Effect.Pools).Append8DC540(4, callback, 0x6C, 0x269AAE0, (_, _, _) => new(2, 4));
        scan = scan with { Effect = scan.Effect with { Pools = child.Hierarchy.Pools } };
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 16).ToArray();
        bool[] known = Enumerable.Repeat(true, 16).ToArray();
        known[14] = false;
        known[15] = false;
        NativeBattleScanByteOwner mesh = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        return (new(scan, 1, new Dictionary<uint, NativeBattleScanByteOwner> { [0x15CDDEC] = mesh, [0x15CE1D4] = mesh }.ToImmutableDictionary()), child.Address);
    }
}
