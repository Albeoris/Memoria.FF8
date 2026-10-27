using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8NodeDrawingTests
{
    private const uint MeshAddress = 0x1819000;
    private static NativeBattleActionMatrix Matrix => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleSummonColors Colors => new(0, 0, 1f / 4096, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Fact]
    public void Shared_context_reservation_preserves_unwritten_bytes_and_returns_exact_camera_pointer()
    {
        NativeBattleDoom8Frame frame = Frame();
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectNodeContextMemory.Length).Write(NativeBattleActorWaveMesh.SharedViewOffset + 0x48, [1, 2, 3, 4]);
        frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, view) };
        NativeBattleDoom8Frame prepared = NativeBattleDoom8NodeDrawing.Begin(frame, MeshAddress, 0, 0, null);
        Assert.Equal(NativeBattleActorWaveMesh.ContextAddress, prepared.State.ReadDword(NativeBattleDoom8NodeDrawing.ContextPointer));
        NativeBattleEffectArena context = Context(prepared);
        Assert.Equal(MeshAddress, U32(context, 0));
        Assert.Equal(0x30u, U32(context, 0x14));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, context.Read(0x48, 4));
        Assert.Throws<NotSupportedException>(() => context.Read(8, 4));
        Assert.Throws<NotSupportedException>(() => context.Read(4, 4));
        Assert.Equal(NativeBattleDoom8NodeDrawing.CopiedViewAddress, NativeBattleDoom8NodeDrawing.End(prepared).State.ReadDword(NativeBattleDoom8NodeDrawing.ContextPointer));
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8NodeDrawing.End(frame));
    }

    [Fact]
    public void Accepted_GT4_publishes_geometry_color_context_and_independent_next_state()
    {
        NativeBattleDoom8Frame frame = NativeBattleDoom8NodeDrawing.Begin(Frame(), MeshAddress, 0, 2048, 0xC08040);
        NativeBattleDoom8NodeMeshStep emitted = Emit(frame, Mesh());
        Assert.Equal(52, emitted.Batch.AllocationBytes);
        Assert.NotNull(Assert.Single(emitted.Batch.Attempts).Primitive);
        Assert.Equal((uint?)500, emitted.Frame.Memory.GeometryRegisters!.Otz);
        Assert.Equal((uint?)500, emitted.Frame.Memory.GeometryRegisters.Mac0);
        Assert.Equal((uint?)0x400, emitted.Frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal((uint?)2048, emitted.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(MeshAddress + 8, U32(Context(emitted.Frame), 4));
        Assert.Equal(500u, U32(Context(emitted.Frame), 0x38));
        NativeBattleDoom8Frame restored = RoundTrip(emitted.Frame);
        Assert.Equal(SnapshotJson(Emit(emitted.Frame, Mesh())), SnapshotJson(Emit(restored, Mesh())));
    }

    [Fact]
    public void Rejected_GT4_keeps_fourth_vertex_and_average_depth_unwritten()
    {
        NativeBattleDoom8Frame frame = Frame();
        frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectNodeContextMemory.Length)) };
        frame = NativeBattleDoom8NodeDrawing.Begin(frame, MeshAddress, 1, 0, null);
        NativeBattleDoom8NodeMeshStep emitted = Emit(frame, Mesh(reverse: true));
        Assert.Equal(0, emitted.Batch.AllocationBytes);
        Assert.Null(Assert.Single(emitted.Batch.Attempts).Primitive);
        Assert.Null(emitted.Frame.Memory.GeometryRegisters!.Otz);
        Assert.Throws<NotSupportedException>(() => Context(emitted.Frame).Read(0x38, 4));
        Assert.Equal(NativeBattleActorWaveMesh.ContextAddress, emitted.Frame.State.ReadDword(NativeBattleDoom8NodeDrawing.ContextPointer));
    }

    [Fact]
    public void Unsupported_scratch_pointer_or_missing_geometry_owner_is_not_replaced()
    {
        NativeBattleDoom8Frame frame = Frame();
        NativeBattleDoom8Frame invalid = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8NodeDrawing.Begin(invalid with { State = invalid.State.WriteDword(NativeBattleDoom8NodeDrawing.ContextPointer, 0) }, MeshAddress, 0, 0, null));
        frame = NativeBattleDoom8NodeDrawing.Begin(frame, MeshAddress, 0, 0, null);
        Assert.Throws<NotSupportedException>(() => Emit(frame with { Memory = frame.Memory with { GeometryRegisters = null } }, Mesh()));
    }

    private static NativeBattleDoom8NodeMeshStep Emit(NativeBattleDoom8Frame frame, ImmutableArray<byte> mesh) => NativeBattleDoom8NodeDrawing.Emit(frame, mesh, MeshAddress, Matrix, new(null, 100, 160, 108), Colors, 0x4321);

    private static NativeBattleDoom8Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { ScriptedGraphics = memory.ScriptedGraphics!.InitializeGeom56BAA0() };
        return new(memory, effect, new(1), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()).WriteDword(NativeBattleDoom8NodeDrawing.ContextPointer, NativeBattleDoom8NodeDrawing.CopiedViewAddress));
    }

    private static NativeBattleEffectArena Context(NativeBattleDoom8Frame frame) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectNodeContextMemory.Capture(frame.Memory), NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
    private static uint U32(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));

    private static ImmutableArray<byte> Mesh(bool reverse = false)
    {
        byte[] mesh = new byte[108];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 4);
        short[] vertices = [-400, -400, 0, 1, 400, -400, 0, 2, -400, 400, 0, 3, 400, 400, 0, 4];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 2), vertices[index]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(68), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(72), 0x3C332211);
        ushort[] offsets = reverse ? [0, 4, 2, 6] : [0, 2, 4, 6];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(76 + index * 2), offsets[index]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(84), 0x12340302);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(88), 0x56780605);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(92), 0x0A090807);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(96), 0xA0554422);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(100), 0xB0886633);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(104), 0xC0BB9944);
        return mesh.ToImmutableArray();
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
