using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79ParticleDrawingTests
{
    [Theory]
    [InlineData(-4096)]
    [InlineData(0)]
    [InlineData(2048)]
    [InlineData(8192)]
    public void Scaled_head_scales_transposed_view_before_current_GTE_composition_and_preserves_padding(short scale)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x1C, 100).WriteWord(0x1E, unchecked((ushort)-25)).WriteWord(0x20, 2000);
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 232).Write(0xCA, [0x56, 0x34]);
        NativeBattleProjectedSpriteHead result = NativeBattleEffect79ParticleDrawing.PrepareHead(Memory(), task, View(), temporary, scale);
        Assert.Equal(new short[] { scale, 0, 0, 0, scale, 0, 0, 0, scale }, Enumerable.Range(0, 9).Select(index => S(result.TemporaryD8, 0xB8 + index * 2)));
        Assert.Equal(new int?[] { 100, -25, 2000 }, result.Memory.GeometryRegisters!.Translation);
        Assert.Equal(new byte?[] { 0x56, 0x34 }, result.Memory.GeometryRegisters.Rotation20.Skip(18));
        Assert.Equal(new[] { (int)scale, scale, scale }, new[] { I(result.TemporaryD8, 0xD8), I(result.TemporaryD8, 0xDC), I(result.TemporaryD8, 0xE0) });
        Assert.DoesNotContain(true, result.TemporaryD8.ReadSnapshot(0xE4, 4).Known);
        Assert.Equal(0x1580B30u, U(result.TemporaryD8, 0));
        Assert.Equal(-64, S(result.TemporaryD8, 0xB4));
    }

    [Fact]
    public void Unit_scale_retains_unwritten_scale_scratch_and_matches_the_ordinary_head()
    {
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 232);
        NativeBattleProjectedSpriteHead result = NativeBattleEffect79ParticleDrawing.PrepareHead(Memory(), Task(), View(), temporary, 4096);
        NativeBattleProjectedSpriteHead expected = NativeBattleProjectedSpriteD8.PrepareHead(Memory(), Task(), View(), temporary);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)));
        Assert.DoesNotContain(true, result.TemporaryD8.ReadSnapshot(0xD8, 16).Known);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Drawing_uses_actual_heap_size_private_cursor_repeated_bias_and_extended_packet_sidecars(bool scaled)
    {
        NativeBattleEffect79Frame frame = Frame(Task().WriteWord(0x20, 2000));
        NativeBattleEffect79Resources resources = Resources();
        NativeBattleGfGeometryBatch? delivered = null;
        int length = scaled ? 232 : 216;
        NativeBattleEffect79Inputs inputs = Inputs((current, operation) =>
        {
            NativeBattleEffect79DeliverBatch delivery = Assert.IsType<NativeBattleEffect79DeliverBatch>(operation);
            Assert.Equal(0x25DF33Cu, delivery.CursorGlobal);
            Assert.Equal(NativeBattleTemporaryMemory.Address + (uint)length, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            delivered = delivery.Batch;
            return current with { State = current.State.WriteDword(delivery.CursorGlobal, checked(current.State.ReadDword(delivery.CursorGlobal) + (uint)delivery.Batch.AllocationBytes)) };
        });
        NativeBattleEffect79Frame result = scaled ? NativeBattleEffect79ParticleDrawing.Draw7BC950(frame, 0x200, resources, inputs, 8192) : NativeBattleEffect79ParticleDrawing.Draw7BBEB0(frame, 0x200, resources, inputs);
        Assert.NotNull(delivered);
        Assert.Equal(92, delivered!.AllocationBytes);
        NativeBattleGeometryPrimitive[] quads = delivered.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 10).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(new[] { 126, 110 }, quads.Select(quad => quad.Bucket));
        NativeBattleGfGeometryAttempt environment = Assert.Single(delivered.Attempts.Where(attempt => attempt.Primitive?.Words.Length == 2));
        Assert.Equal(40, environment.ByteOffsetRelative);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, environment.Primitive!.VertexPayload4);
        Assert.Equal(0xE1000220u, environment.Primitive.Words[1]);
        Assert.Equal(372, BinaryPrimitives.ReadInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Address + 0x3C, 4)));
        Assert.Equal(500u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(0x20E0000u + 92, result.State.ReadDword(0x25DF33C));
        Assert.Equal(0x2793E58u, result.State.ReadDword(0x25EB3AC));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.All(delivered.Attempts.Where(attempt => attempt.Data.Length == 40 && attempt.WriteMask[4] == NativeBattleGfPacketByteState.Known), attempt =>
        {
            Assert.Equal(0x2C646464u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
        });
        NativeBattleEffect79Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect79Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        NativeBattleEffect79Frame Next(NativeBattleEffect79Frame current) => NativeBattleEffect79ParticleDrawing.Draw7BBEB0(current, 0x200, resources, inputs);
        if (!scaled)
        {
            Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(result))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Next(restored))));
        }
    }

    [Fact]
    public void Hidden_particle_returns_before_heap_resource_view_and_GTE_consumers()
    {
        NativeBattleEffect79Frame frame = Frame(Task().WriteByte(0x26, 4)) with { Staging = null };
        Assert.Same(frame, NativeBattleEffect79ParticleDrawing.Draw7BBEB0(frame, 0x200, null!, null!));
        Assert.Same(frame, NativeBattleEffect79ParticleDrawing.Draw7BC950(frame, 0x200, null!, null!, 8192));
    }

    private static NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(0, 0, null, 0x7BBC20, new byte[168].ToImmutableArray()).WriteDword(0x4C, 0x1580B30).WriteWord(0x56, unchecked((ushort)-64));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattlePackedMatrix32 View() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0x1234);
    private static NativeBattleEffect79Frame Frame(NativeBattleModulePoolTask task)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 180, 1);
        pool.WriteSlot(0, task);
        NativeBattleOriginalEffectState effect = new(79, 78, new(79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79Profile.Initializer), null, null) { Pools = [pool] };
        return new(Memory(), effect, new(1), NativeBattleEffect79State.FromColdProcess().WriteDword(0x25DF33C, 0x20E0000).WriteDword(0x25EB3AC, 0x2793E58)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleEffect79Inputs Inputs(Func<NativeBattleEffect79Frame, NativeBattleEffect79Operation, NativeBattleEffect79Frame> execute) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException(), _ => View(), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => 0, (_, _) => throw new InvalidOperationException(), execute);
    private static NativeBattleEffect79Resources Resources()
    {
        byte[] bytes = new byte[58];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 0x80000002);
        for (int quad = 0; quad < 2; quad++)
        {
            Span<byte> row = bytes.AsSpan(18 + quad * 20, 20);
            row[0] = row[2] = 2;
            row[1] = 100;
            row[3] = 0x2C;
            row[4] = 10;
            row[5] = 20;
        }
        return new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleEffect79NodeResourceBank>.Empty, null!, ImmutableDictionary<short, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, uint>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x1580B30, bytes.ToImmutableArray()), [], 0);
    }
    private static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static int I(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
}
