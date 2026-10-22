using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8StripTests
{
    [Fact]
    public void Target_initialization_owns_exact58_bytes_and_three17_by4_banks_preserving_unknown_W()
    {
        var fixture = Fixture();
        NativeBattleDoom8Frame frame = fixture.Frame;
        foreach (uint bank in new[] { NativeBattleDoom8Strips.BaseVertices, NativeBattleDoom8Strips.NearVertices, NativeBattleDoom8Strips.FarVertices })
        {
            frame = frame with { State = frame.State with { Globals = frame.State.Globals.MarkUnknown(checked((int)(bank - NativeBattleDoom8State.BaseAddress)), 544) } };
        }
        List<NativeBattleDoom8Operation> calls = [];
        frame = NativeBattleDoom8Strips.InitializeTarget8C8120(frame, fixture.Address, 3, Tables(), (current, operation) =>
        {
            calls.Add(operation);
            if (operation is NativeBattleDoom8StripAnchor anchor)
            {
                Assert.Equal(3, anchor.Actor);
                Assert.Equal(0x275F548u, anchor.Destination);
                return current with { State = current.State.WriteWord(anchor.Destination, 0).WriteWord(anchor.Destination + 2, 0).WriteWord(anchor.Destination + 4, 4096) };
            }
            NativeBattleDoom8StripLookBasis basis = Assert.IsType<NativeBattleDoom8StripLookBasis>(operation);
            Assert.Equal(new NativeBattleVector(0, 0, 4096), basis.Direction);
            return current;
        });
        Assert.IsType<NativeBattleDoom8StripAnchor>(calls[0]);
        Assert.IsType<NativeBattleDoom8StripLookBasis>(calls[1]);
        Assert.Equal(4096u, frame.State.ReadDword(0x275F55C));
        Assert.Equal(4096u, frame.State.ReadDword(0x275F538));
        Assert.Equal((ushort)0, frame.State.ReadWord(0x275F522));
        Assert.Equal(new short[] { -3, -1, 240 }, Vector(frame, NativeBattleDoom8Strips.NearVertices + 32));
        Assert.Equal(new short[] { 0, -4, 240 }, Vector(frame, NativeBattleDoom8Strips.FarVertices + 32));
        Assert.Equal(new short[] { 3, 0, 3840 }, Vector(frame, NativeBattleDoom8Strips.BaseVertices + 16 * 32 + 24));
        foreach (uint bank in new[] { NativeBattleDoom8Strips.BaseVertices, NativeBattleDoom8Strips.NearVertices, NativeBattleDoom8Strips.FarVertices })
        {
            Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(bank + 6));
            Assert.Throws<NotSupportedException>(() => frame.State.ReadWord(bank + 542));
        }
    }

    [Fact]
    public void Placement_reads_endpoint17_and_preserves_taskW_without_GTE_or_random_side_effects()
    {
        var fixture = Fixture();
        NativeBattleDoom8Frame frame = Initialize(fixture.Frame, fixture.Address);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, fixture.Address).MarkPayloadUnknown(0x22, 2);
        frame = NativeBattleDoom8Controller.Write(frame, fixture.Address, task);
        NativeBattleEffectMemory memory = frame.Memory;
        NativeBattleDoom8Frame placed = NativeBattleDoom8Strips.Place8C8350(frame, fixture.Address, 17, Tables());
        Assert.Equal(new short[] { 0, -17, 4080 }, TaskVector(placed, fixture.Address));
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Controller.Read(placed, fixture.Address).ReadWord(0x22));
        Assert.Same(memory, placed.Memory);
        Assert.Equal(frame.Random, placed.Random);
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Strips.Place8C8350(frame, fixture.Address, 18, Tables()));
    }

    [Fact]
    public void Two_strip_banks_emit_every_quad_to_COMMON_without_culling_and_preserve_unwritten_color_codes()
    {
        var fixture = Fixture();
        NativeBattleDoom8Frame frame = Initialize(fixture.Frame, fixture.Address);
        List<NativeBattleGfGeometryBatch> packets = [];
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        NativeBattleDoom8Step result = NativeBattleDoom8Strips.AdvanceStrips8C84A0(frame, Tables(), camera, new(null, 100, 160, 108), (current, batch) =>
        {
            packets.Add(batch);
            Assert.Equal(0u, current.Memory.ScriptedColorRegisters!.Flags);
            return current with { State = current.State.WriteDword(0x275F9D4, (uint)packets.Count) };
        });
        Assert.Equal(1, result.Result);
        Assert.Equal(6, packets.Count);
        Assert.Equal(6u, result.Frame.State.ReadDword(0x275F9D4));
        Assert.Equal((ushort)1, result.Frame.State.ReadWord(0x275F564));
        foreach (NativeBattleGfGeometryBatch batch in packets)
        {
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(36, batch.AllocationBytes);
            Assert.Equal((byte)0x3A, attempt.Data[7]);
            foreach (int index in new[] { 15, 23, 31 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]);
            }
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            Assert.InRange(primitive.Bucket, 0, 65535);
            Assert.Equal(4, primitive.VertexPayload4.Length);
        }
        Assert.Throws<NotSupportedException>(() => result.Frame.State.ReadDword(NativeBattleDoom8Strips.ColorsAddress + 4));
        Assert.Equal(new byte[] { 6, 0, 2 }, result.Frame.State.Read(NativeBattleDoom8Strips.ColorsAddress + 16 + 4, 3));
        Assert.Equal(0u, result.Frame.State.ReadDword(NativeBattleDoom8Strips.ColorsAddress));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Null(result.Frame.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Sentinel_does_not_read_camera_or_registers_and_restored_next_draw_is_identical()
    {
        var fixture = Fixture();
        NativeBattleDoom8Frame frame = Initialize(fixture.Frame, fixture.Address);
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = -1 }, null);
        NativeBattleDoom8Frame restored = JsonSerializer.Deserialize<NativeBattleDoom8Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleDoom8Step Draw(NativeBattleDoom8Frame current) => NativeBattleDoom8Strips.AdvanceStrips8C84A0(current, Tables(), camera, new(null, 100, 160, 108), (next, _) => next);
        Assert.Equal(JsonSerializer.Serialize(Draw(frame)), JsonSerializer.Serialize(Draw(restored)));
        frame = frame with { State = frame.State.WriteWord(NativeBattleDoom8Strips.TargetAddress + 0x54, 13), Memory = frame.Memory with { GeometryRegisters = null, ScriptedColorRegisters = null } };
        NativeBattleDoom8Step done = NativeBattleDoom8Strips.AdvanceStrips8C84A0(frame, Tables(), null!, null!, (_, _) => throw new InvalidOperationException());
        Assert.Equal(0, done.Result);
        Assert.Same(frame, done.Frame);
    }

    private static NativeBattleDoom8StripTables Tables() => new([-3, -1, 1, 3], Enumerable.Range(0, 18).Select(index => (short)-index).ToImmutableArray(), Enumerable.Range(0, 13).Select(_ => new NativeBattleDoom8StripRange(0, 1)).Append(new(127, 127)).ToImmutableArray());
    private static short[] Vector(NativeBattleDoom8Frame frame, uint address) => Enumerable.Range(0, 3).Select(axis => unchecked((short)frame.State.ReadWord(address + (uint)(axis * 2)))).ToArray();
    private static short[] TaskVector(NativeBattleDoom8Frame frame, uint address) => Enumerable.Range(0, 3).Select(axis => unchecked((short)NativeBattleDoom8Controller.Read(frame, address).ReadWord(0x1C + axis * 2))).ToArray();

    private static NativeBattleDoom8Frame Initialize(NativeBattleDoom8Frame frame, uint address)
    {
        return NativeBattleDoom8Strips.InitializeTarget8C8120(frame, address, 3, Tables(), (current, operation) => operation is NativeBattleDoom8StripAnchor anchor ? current with { State = current.State.WriteWord(anchor.Destination, 0).WriteWord(anchor.Destination + 2, 0).WriteWord(anchor.Destination + 4, 4096) } : current);
    }

    private static (NativeBattleDoom8Frame Frame, uint Address) Fixture()
    {
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(4, 0x8C8050, 1112);
        hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteByte(0x2D, 3));
        NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleDoom8Frame frame = new(memory, effect, new(123), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray())) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        return (frame, appended.Address);
    }
}
