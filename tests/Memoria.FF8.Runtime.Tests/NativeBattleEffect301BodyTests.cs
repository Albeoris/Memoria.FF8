using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect301Lower;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect301LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect301BodyTests
{
    [Fact]
    public void Actual_loaded_pointer_epoch_survives_new_views_and_changes_on_same_address_resource_replacement()
    {
        NativeBattleModelCpuReference loaded = new(3, 100);
        NativeBattleResourcePointer first = new(loaded, null, 128);
        NativeBattleResourcePointer freshView = new(loaded, null, 128);
        NativeBattleResourcePointer replacement = new(new(3, 100), null, 128);
        long epoch = NativeBattleEffect32Presentation.ResourceEpoch(first);
        Assert.Equal(epoch, NativeBattleEffect32Presentation.ResourceEpoch(freshView));
        long replaced = NativeBattleEffect32Presentation.ResourceEpoch(replacement);
        Assert.NotEqual(epoch, replaced);
        NativeBattlePilotVisualIdentity owner = new(301, 0x123400, 1, 11, 0) { ResourceGeneration = epoch };
        var a = Draw(new Fixture(), Geometry(false), Context(Fixture.Main + 0x1494, 1), pilotIdentity: owner);
        var b = Draw(new Fixture(), Geometry(false), Context(Fixture.Main + 0x1494, 1), pilotIdentity: owner with { ResourceGeneration = replaced });
        var firstPacket = Assert.Single(a.Batch.Attempts);
        var secondPacket = Assert.Single(b.Batch.Attempts);
        Assert.Equal(firstPacket.Data.ToArray(), secondPacket.Data.ToArray());
        Assert.Equal(firstPacket.WriteMask.ToArray(), secondPacket.WriteMask.ToArray());
        var firstGeometry = firstPacket.Primitive!.PilotGeometry!;
        var secondGeometry = secondPacket.Primitive!.PilotGeometry!;
        Assert.Equal(firstGeometry.Identity.Generation, secondGeometry.Identity.Generation);
        Assert.NotEqual(firstGeometry.Identity.ResourceGeneration, secondGeometry.Identity.ResourceGeneration);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_body_uses_detached_native_state_and_actual_cache_depths(bool quad)
    {
        Fixture native = new();
        Fixture pilot = new();
        var plain = Draw(native, Geometry(quad), Context(Fixture.Main + 0x1494, 1));
        string frozen = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain.Frame));
        var captured = Draw(pilot, Geometry(quad), Context(Fixture.Main + 0x1494, 1), pilotIdentity: new(301, 0x123400, 1, 11, 0) { ResourceGeneration = 31 });
        Assert.Equal(frozen, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Frame)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain.Context136)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Context136)));
        NativeBattleGfGeometryAttempt expected = Assert.Single(plain.Batch.Attempts);
        NativeBattleGfGeometryAttempt actual = Assert.Single(captured.Batch.Attempts);
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.WriteMask, actual.WriteMask);
        Assert.Equal(expected.Primitive!.Words, actual.Primitive!.Words);
        Assert.Equal(expected.Primitive.VertexPayload4, actual.Primitive.VertexPayload4);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitive.PilotGeometry);
        Assert.Equal((ushort)301, geometry.Identity.Effect);
        Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
        Assert.False(geometry.ScreenSpace);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(actual.Primitive.VertexPayload4[0], geometry.SourceDepths!.Value.X);
        Assert.Equal(1000, geometry.A.Z);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Body_uses_current_composed_bones_retained_cache_W_and_actual_packet_depth(bool quad)
    {
        Fixture fixture = new();
        byte[] source = Geometry(quad);
        uint scratch = Fixture.Main + 0x1494;
        fixture.Frame = WriteBytes(fixture.Frame, scratch, NativeBattleEffectArena.Clear().MarkUnknown(14, 2).MarkUnknown(30, 2).MarkUnknown(46, 2).MarkUnknown(62, 2), 64);
        NativeBattleEffect301BodyStep result = Draw(fixture, source, Context(scratch, age: 1));
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Equal(quad ? 40 : 32, result.Batch.AllocationBytes);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(17 + 62, packet.Primitive.Bucket);
        Assert.Equal(new[] { 250, 250, 250, quad ? 250 : 0 }, packet.Primitive.VertexPayload4);
        Assert.DoesNotContain(true, ReadBytes(result.Frame, scratch + 14, 2).ReadSnapshot(0, 2).Known);
        Assert.Equal(1, ReadBytes(result.Frame, scratch + 7, 1).Read(0, 1)[0]);
        Assert.Equal(2, ReadBytes(result.Frame, scratch + 2 * 16 + 7, 1).Read(0, 1)[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
        Assert.Equal(0xFFFF, unchecked((ushort)Word301(result.Context136, quad ? 10 : 8)));
        Assert.Equal(0xFFFF, unchecked((ushort)Word301(result.Context136, quad ? 8 : 10)));
    }

    [Fact]
    public void Backface_record_trigger_precedes_NCLIP_rejection_and_does_not_insert_map()
    {
        Fixture fixture = new();
        byte[] source = Geometry(false);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(46), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(48), 1);
        NativeBattleEffect301BodyStep result = Draw(fixture, source, Context(Fixture.Main + 0x1494, age: 0));
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleEffectArena row = ReadBytes(result.Frame, Fixture.Main + 0xAC, 8);
        Assert.Equal(12, Word301(row, 6));
        Assert.Equal(0, Word301(row, 0));
        Assert.Equal(1000, Word301(row, 2));
        Assert.Equal(255, ReadBytes(result.Frame, Fixture.Main + 0x4AC, 1).Read(0, 1)[0]);
        Assert.True(unchecked((int)result.Frame.Memory.GeometryRegisters!.Mac0!.Value) < 0);
    }

    [Fact]
    public void Fully_classified_upper_faces_reject_before_packet_or_NCLIP_writes()
    {
        Fixture fixture = new();
        NativeBattleEffectArena context = Context(Fixture.Main + 0x1494, age: 0).Write(0x6C, BitConverter.GetBytes(-1000));
        NativeBattleEffect301BodyStep result = Draw(fixture, Geometry(false), context);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.All(packet.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(0, Word301(ReadBytes(result.Frame, Fixture.Main + 0xB2, 2), 0));
    }

    [Fact]
    public void Saturated_IR_deformation_keeps_full_MAC_and_cache_padding_unknown()
    {
        Fixture fixture = new();
        byte[] source = Geometry(false);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(14), 20000);
        fixture.Frame = WriteBytes(fixture.Frame, Fixture.Main + 0x1494, NativeBattleEffectArena.Clear().MarkUnknown(14, 2), 16);
        NativeBattleActionMatrix bone = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        NativeBattleEffect301BodyStep result = Draw(fixture, source, Context(Fixture.Main + 0x1494, 1), bone);
        Assert.Equal(short.MaxValue, Word301(ReadBytes(result.Frame, Fixture.Main + 0x1494, 16), 8));
        Assert.DoesNotContain(true, ReadBytes(result.Frame, Fixture.Main + 0x1494 + 14, 2).ReadSnapshot(0, 2).Known);
    }

    [Fact]
    public void Mask_zero_does_not_consume_bones_vertex_cache_or_face_payload()
    {
        Fixture fixture = new();
        byte[] source = Geometry(false);
        NativeBattleEffectArena context = Context(Fixture.Main + 0x1494, 0).Write(24, new byte[4]);
        NativeBattleEffect301BodyStep result = Draw(fixture, source, context);
        Assert.Empty(result.Batch.Attempts);
        Assert.Same(fixture.Frame.Memory.GeometryRegisters, result.Frame.Memory.GeometryRegisters);
        Assert.Equal(0u, Dword301(result.Context136, 0x84));
    }

    internal static NativeBattleEffect301BodyStep Draw(Fixture fixture, byte[] source, NativeBattleEffectArena context, NativeBattleActionMatrix? bone = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return NativeBattleEffect301BodyDrawing.DrawLoaded(fixture.Frame, (offset, count) => source.AsSpan(offset, count).ToArray(), 0x1234000, [NativeBattlePackedMatrix32.FromMatrix(bone ?? NativeBattleCuragaMatrices.Identity8DD770(), null)], context, NativeBattleTemporaryMemory.Address, 0, fixture.Resources, fixture.Inputs() with { ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108) }, pilotIdentity);
    }
    internal static NativeBattleEffectArena Context(uint scratch, int age)
    {
        return Put301(Put301(Put301(Put301(Put301(Put301(PutWord301(PutWord301(NativeBattleEffectArena.Clear(), 16, 320), 18, 216), 4, scratch), 20, 0x00112233), 24, uint.MaxValue), 0x6C, 64), 0x70, (uint)age), 0x74, 500).MarkUnknown(0x7C, 4);
    }
    internal static byte[] Geometry(bool quad)
    {
        int vertices = quad ? 4 : 3;
        int header = (14 + vertices * 6 + 3) & ~3;
        byte[] bytes = new byte[header + 12 + (quad ? 20 : 16)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), (short)vertices);
        short[] xyz = [-100, -100, 1000, 100, -100, 1000, 0, 100, 1000, 100, 100, 1000];
        for (int index = 0; index < vertices * 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 2), xyz[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(header + (quad ? 2 : 0)), 1);
        int face = header + 12;
        for (int corner = 0; corner < vertices; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + corner * 2), (ushort)corner);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 8), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x03450605);
        return bytes;
    }
}
