using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGastricJuice128TrailTests
{
    [Fact]
    public void Two_points_emit_native_partial_GT4_with_signed_width_depth_and_no_sidecar()
    {
        NativeBattleGastricJuice128Frame frame = Frame();
        frame = Point(frame, 0, 100, 200, 512, 32, 2048, Address(1));
        frame = Point(frame, 1, 116, 200, 512, 32, 1024, 0);
        string registers = JsonSerializer.Serialize(new { frame.Memory.GeometryRegisters, frame.Memory.ScriptedColorRegisters });
        var actual = NativeBattleGastricJuice128Trail.DrawRibbon6B90B0(frame, Address(0), 2, Resources());
        Assert.Equal(52, actual.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        Assert.Equal(145, packet.Primitive!.Bucket);
        Assert.Empty(packet.Primitive.VertexPayload4);
        Assert.Equal(0x0C000000u, Dword(packet.Data, 0));
        Assert.Equal(0x3E808080u, Dword(packet.Data, 4));
        Assert.Equal(0x00808080u, Dword(packet.Data, 16));
        Assert.Equal(0x00404040u, Dword(packet.Data, 28));
        Assert.Equal(0x00404040u, Dword(packet.Data, 40));
        Assert.Equal(0x00C70064u, Dword(packet.Data, 8));
        Assert.Equal(0x00C90064u, Dword(packet.Data, 20));
        Assert.Equal(0x00C80074u, Dword(packet.Data, 32));
        Assert.Equal(0x00C80074u, Dword(packet.Data, 44));
        Assert.Equal(0x3D14E048u, Dword(packet.Data, 12));
        Assert.Equal(0x00BAE078u, Dword(packet.Data, 24));
        Assert.Equal((ushort)0xE048, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(36)));
        Assert.Equal((ushort)0xE078, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(48)));
        for (int index = 0; index < 52; index++)
        {
            Assert.Equal(index is 38 or 39 or 50 or 51 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, packet.WriteMask[index]);
        }
        Assert.Equal(registers, JsonSerializer.Serialize(new { actual.Frame.Memory.GeometryRegisters, actual.Frame.Memory.ScriptedColorRegisters }));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(frame.State, actual.Frame.State);
    }

    [Fact]
    public void JSON_restore_preserves_live_chain_unknown_fields_and_packet_result()
    {
        NativeBattleGastricJuice128Frame frame = Frame();
        frame = Point(frame, 0, 100, 200, 512, 32, 2048, Address(1));
        frame = Point(frame, 1, 116, 200, 512, 32, 1024, 0);
        NativeBattleGastricJuice128Frame restored = JsonSerializer.Deserialize<NativeBattleGastricJuice128Frame>(JsonSerializer.Serialize(frame))!;
        var expected = NativeBattleGastricJuice128Trail.DrawRibbon6B90B0(frame, Address(0), 2, Resources());
        var actual = NativeBattleGastricJuice128Trail.DrawRibbon6B90B0(restored, Address(0), 2, Resources());
        Assert.Equal(JsonSerializer.Serialize(expected.Frame), JsonSerializer.Serialize(actual.Frame));
        Assert.Equal(JsonSerializer.Serialize(expected.Batch), JsonSerializer.Serialize(actual.Batch));
    }

    [Fact]
    public void Two_substeps_use_128_64_fade_generate_radial_velocity_and_spawn_one_splash()
    {
        NativeBattleGastricJuice128Frame frame = ActiveFrame();
        uint curve = frame.Effect.Pools[2].BankAddress;
        NativeCrtRandomState random = frame.Random;
        List<NativeBattleGastricJuice128Operation> operations = [];
        (frame, int result) = NativeBattleGastricJuice128Trail.Advance6B8DC0(frame, curve, Resources(), Inputs((current, operation) =>
        {
            operations.Add(operation);
            return current;
        }));
        Assert.Equal(0, result);
        Assert.Equal(random, frame.Random);
        NativeBattleModulePoolTask task = NativeBattleGastricJuice128Controller.Read(frame, curve);
        Assert.Equal((ushort)4096, task.ReadWord(0x1E));
        Assert.Equal((ushort)512, task.ReadWord(0x4A));
        Assert.Equal((ushort)3, task.ReadWord(14));
        Assert.Equal((short)1792, Word(frame.Memory.Arena!, PointsOffset + 0x20));
        Assert.Equal((short)1792, Word(frame.Memory.Arena!, PointsOffset + PointStride + 0x20));
        Assert.Equal((short)1920, Word(frame.Memory.Arena!, PointsOffset + 2 * PointStride + 0x20));
        Assert.Equal((short)1, Word(frame.Memory.Arena!, PointsOffset));
        Assert.Equal((short)2, Word(frame.Memory.Arena!, PointsOffset + 2));
        Assert.Equal((short)3, Word(frame.Memory.Arena!, PointsOffset + 4));
        Assert.Equal((short)500, Word(frame.Memory.Arena!, PointsOffset + PointStride));
        Assert.Equal((short)-736, Word(frame.Memory.Arena!, PointsOffset + PointStride + 2));
        Assert.Equal((short)32, Word(frame.Memory.Arena!, PointsOffset + PointStride + 10));
        Assert.Equal((short)1000, Word(frame.Memory.Arena!, PointsOffset + 2 * PointStride));
        NativeBattleModulePoolTask splash = Assert.Single(frame.Effect.Pools[3].Slots, value => (value.Flags & 1) != 0);
        Assert.Equal(0x6B9030u, splash.Callback);
        Assert.Equal((ushort)1024, splash.ReadWord(0x16));
        Assert.Single(operations.OfType<NativeBattleGastricJuice128DeliverRibbonBatch>());
        NativeBattleGastricJuice128Frame restored = JsonSerializer.Deserialize<NativeBattleGastricJuice128Frame>(JsonSerializer.Serialize(frame))!;
        var expected = NativeBattleGastricJuice128Trail.Advance6B8DC0(frame, curve, Resources(), Inputs((current, _) => current));
        var actual = NativeBattleGastricJuice128Trail.Advance6B8DC0(restored, curve, Resources(), Inputs((current, _) => current));
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(JsonSerializer.Serialize(expected.Frame), JsonSerializer.Serialize(actual.Frame));
    }

    [Fact]
    public void Negative_tag_suppresses_splash_and_depleted_pair_retires_without_freeing_last_record()
    {
        NativeBattleGastricJuice128Frame frame = ActiveFrame();
        uint curve = frame.Effect.Pools[2].BankAddress;
        NativeBattleModulePoolTask task = NativeBattleGastricJuice128Controller.Read(frame, curve).WriteWord(0x48, ushort.MaxValue);
        frame = NativeBattleGastricJuice128Controller.Write(frame, curve, task);
        (frame, _) = NativeBattleGastricJuice128Trail.Advance6B8DC0(frame, curve, Resources(), Inputs((current, _) => current));
        Assert.All(frame.Effect.Pools[3].Slots, value => Assert.Equal(0, value.Flags & 1));
        for (int index = 0; index < 3; index++)
        {
            frame = NativeBattleGastricJuice128Controller.WriteArena(frame, Address(index) + 0x20, BitConverter.GetBytes((short)64));
        }
        (frame, int result) = NativeBattleGastricJuice128Trail.Advance6B8DC0(frame, curve, Resources(), Inputs((current, _) => current));
        Assert.Equal(2, result);
        Assert.Equal((ushort)1, NativeBattleGastricJuice128Controller.Read(frame, curve).ReadWord(14));
        Assert.Equal((short)-1, Word(frame.Memory.Arena!, PointsOffset + 6));
        Assert.Equal((short)-1, Word(frame.Memory.Arena!, PointsOffset + PointStride + 6));
        Assert.Equal((short)64, Word(frame.Memory.Arena!, PointsOffset + 2 * PointStride + 6));
    }

    private static NativeBattleGastricJuice128Inputs Inputs(Func<NativeBattleGastricJuice128Frame, NativeBattleGastricJuice128Operation, NativeBattleGastricJuice128Frame> execute) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, 0), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), execute);

    private static NativeBattleGastricJuice128Frame ActiveFrame()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < PointCount; index++)
        {
            arena = arena.Write(PointsOffset + index * PointStride + 6, BitConverter.GetBytes(ushort.MaxValue));
        }
        ImmutableArray<NativeBattleEffectTaskPool> pools = [NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1), NativeBattleEffectTaskPool.Initialize(MainDescriptor, ArenaAddress + MainOffset, MainStride, MainCount), NativeBattleEffectTaskPool.Initialize(CurveDescriptor, ArenaAddress + CurveOffset, CurveStride, CurveCount), NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, ArenaAddress, SpriteStride, SpriteCount)];
        NativeBattleOriginalEffectState effect = new(128, 127, new(128, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleGastricJuice128Frame frame = new(memory, effect, new(1), NativeBattleGastricJuice128State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress));
        (frame, uint head) = NativeBattleGastricJuice128Controller.AllocatePoint(frame, NativeBattleGastricJuice128Controller.PointView(new(0, 0, 0)), 7);
        frame = NativeBattleGastricJuice128Controller.WriteArena(frame, head + 8, new byte[] { 1, 0, 2, 0, 3, 0 });
        (frame, uint address) = NativeBattleGastricJuice128Controller.Append(frame, 2, CurveCallback);
        NativeBattleModulePoolTask task = NativeBattleGastricJuice128Controller.Read(frame, address).WriteBytes(12, new byte[68]).WriteWord(14, 1).WriteDword(0x10, head).WriteDword(0x14, head).WriteWord(0x20, 1000).WriteWord(0x26, 2048).WriteWord(0x48, 7).WriteWord(0x4C, 64);
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x28 + index * 2, index is 0 or 4 or 8 ? (ushort)4096 : (ushort)0);
        }
        return NativeBattleGastricJuice128Controller.Write(frame, address, task);
    }

    private static uint Address(int index) => ArenaAddress + (uint)(PointsOffset + index * PointStride);
    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleGastricJuice128Resources Resources(byte[]? table = null) => new(new(MainActAddress, []), new(0, SpriteAAddress, 0, 0, []), new(0, SpriteBAddress, 0, 0, []), new(0, SpriteCAddress, 0, 0, []), new("unused", []), 0, (table ?? Enumerable.Repeat((byte)16, 128 * 128).ToArray()).ToImmutableArray());

    private static NativeBattleGastricJuice128Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(128, 127, new(128, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x246E8) };
        return new(memory, effect, new(1), NativeBattleGastricJuice128State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress));
    }

    private static NativeBattleGastricJuice128Frame Point(NativeBattleGastricJuice128Frame frame, int index, short x, short y, short depth, short width, short? intensity, uint next)
    {
        int offset = PointsOffset + index * PointStride;
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(offset + 6, BitConverter.GetBytes(width)).Write(offset + 0x10, BitConverter.GetBytes(x)).Write(offset + 0x12, BitConverter.GetBytes(y)).Write(offset + 0x14, BitConverter.GetBytes(depth)).Write(offset + 0x24, BitConverter.GetBytes(next));
        if (intensity is { } value)
        {
            arena = arena.Write(offset + 0x20, BitConverter.GetBytes(value));
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }
}
