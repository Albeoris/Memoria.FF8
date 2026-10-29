using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDeathNodeDrawingTests
{
    private const uint MeshAddress = 0x1819000;
    private static NativeBattleActionMatrix Matrix => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleSummonColors Colors => new(0, 0, 1f / 4096, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Shared_context_reservation_preserves_unwritten_bytes_and_returns_exact_camera_pointer(ushort effect)
    {
        NativeBattleDeathFrame frame = Frame(effect);
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectNodeContextMemory.Length).Write(NativeBattleActorWaveMesh.SharedViewOffset + 0x48, [1, 2, 3, 4]);
        frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, view) };
        NativeBattleDeathFrame prepared = NativeBattleDeathNodeDrawing.Begin(frame, MeshAddress, 0, 0, null);
        Assert.Equal(NativeBattleActorWaveMesh.ContextAddress, Pointer(prepared));
        NativeBattleEffectArena context = Context(prepared);
        Assert.Equal(MeshAddress, U32(context, 0));
        Assert.Equal(0x30u, U32(context, 0x14));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, context.Read(0x48, 4));
        Assert.Throws<NotSupportedException>(() => context.Read(8, 4));
        Assert.Throws<NotSupportedException>(() => context.Read(4, 4));
        Assert.Equal(NativeBattleDeathNodeDrawing.CopiedViewAddress, Pointer(NativeBattleDeathNodeDrawing.End(prepared)));
        Assert.Throws<InvalidDataException>(() => NativeBattleDeathNodeDrawing.End(frame));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Accepted_GT4_publishes_geometry_color_context_and_independent_next_state(ushort effect)
    {
        NativeBattleDeathFrame frame = NativeBattleDeathNodeDrawing.Begin(Frame(effect), MeshAddress, 0, 2048, 0xC08040);
        NativeBattleDeathNodeMeshStep emitted = Emit(frame, Mesh());
        Assert.Equal(52, emitted.Batch.AllocationBytes);
        Assert.NotNull(Assert.Single(emitted.Batch.Attempts).Primitive);
        Assert.Equal((uint?)500, emitted.Frame.Memory.GeometryRegisters!.Otz);
        Assert.Equal((uint?)500, emitted.Frame.Memory.GeometryRegisters.Mac0);
        Assert.Equal((uint?)0x400, emitted.Frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal((uint?)2048, emitted.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(MeshAddress + 8, U32(Context(emitted.Frame), 4));
        Assert.Equal(500u, U32(Context(emitted.Frame), 0x38));
        Assert.Equal((byte?)0x21, emitted.Frame.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Equal((byte?)0x43, emitted.Frame.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal((short)4, BinaryPrimitives.ReadInt16LittleEndian(emitted.Frame.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.True(emitted.Frame.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        NativeBattleDeathFrame restored = RoundTrip(emitted.Frame);
        Assert.Equal(SnapshotJson(Emit(emitted.Frame, Mesh())), SnapshotJson(Emit(restored, Mesh())));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Rejected_GT4_keeps_fourth_vertex_and_average_depth_unwritten(ushort effect)
    {
        NativeBattleDeathFrame frame = Frame(effect);
        frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectNodeContextMemory.Length)) };
        frame = NativeBattleDeathNodeDrawing.Begin(frame, MeshAddress, 1, 0, null);
        NativeBattleDeathNodeMeshStep emitted = Emit(frame, Mesh(reverse: true));
        Assert.Equal(0, emitted.Batch.AllocationBytes);
        Assert.Null(Assert.Single(emitted.Batch.Attempts).Primitive);
        Assert.Null(emitted.Frame.Memory.GeometryRegisters!.Otz);
        Assert.Throws<NotSupportedException>(() => Context(emitted.Frame).Read(0x38, 4));
        Assert.Equal(NativeBattleActorWaveMesh.ContextAddress, Pointer(emitted.Frame));
    }

    [Theory]
    [InlineData((ushort)14)]
    [InlineData((ushort)74)]
    public void Unsupported_scratch_pointer_or_missing_geometry_owner_is_not_replaced(ushort effect)
    {
        NativeBattleDeathFrame frame = Frame(effect);
        NativeBattleDeathFrame invalid = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        Assert.Throws<NotSupportedException>(() => NativeBattleDeathNodeDrawing.Begin(WithPointer(invalid, 0), MeshAddress, 0, 0, null));
        frame = NativeBattleDeathNodeDrawing.Begin(frame, MeshAddress, 0, 0, null);
        Assert.Throws<NotSupportedException>(() => Emit(frame with { Memory = frame.Memory with { GeometryRegisters = null } }, Mesh()));
    }

    [Theory]
    [InlineData((ushort)14, false)]
    [InlineData((ushort)14, true)]
    [InlineData((ushort)74, false)]
    [InlineData((ushort)74, true)]
    public void Camera_copy_publishes_both_module_pointers_without_hydrating_padding(ushort effect, bool knownPadding)
    {
        NativeBattleDeathFrame frame = Frame(effect);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(Matrix, knownPadding ? (ushort)0xBEEF : null);
        frame = NativeBattleDeathNodeDrawing.PublishCamera(frame, packed);
        Assert.Equal(packed, frame.Memory.SharedEffectView2793E58);
        Assert.Equal(knownPadding, frame.Memory.SharedEffectView2793E58!.Known[18]);
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect);
        Assert.Equal(NativeBattleDeathNodeDrawing.CopiedViewAddress, Pointer(frame));
        Assert.Equal(NativeBattleDeathNodeDrawing.CopiedViewAddress, U32(profile.RequireState(frame.Memory).Module, profile.ModuleOffset(0x273AFE8)));
        NativeBattleDeathNodeMeshStep emitted = NativeBattleDeathNodeDrawing.Emit(NativeBattleDeathNodeDrawing.Begin(frame, MeshAddress, 0, 2048, 0xC08040), Mesh(), MeshAddress, Matrix, new(null, 100, 160, 108), Colors, null);
        Assert.Null(emitted.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(emitted.Frame.Memory.GeometryRegisters.Rotation20[19]);
    }

    private static NativeBattleDeathNodeMeshStep Emit(NativeBattleDeathFrame frame, ImmutableArray<byte> mesh) => NativeBattleDeathNodeDrawing.Emit(frame, mesh, MeshAddress, Matrix, new(null, 100, 160, 108), Colors, 0x4321);

    private static NativeBattleDeathFrame Frame(ushort effect)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(effect);
        NativeBattleOriginalEffectState original = new(effect, effect, new(effect, profile.Entry, profile.Initializer), null, null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = profile.WithState(memory, NativeBattleDeathState.FromColdProcess());
        memory = memory with { ScriptedGraphics = memory.ScriptedGraphics!.InitializeGeom56BAA0() };
        return NativeBattleDeathNodeDrawing.PublishCamera(new(memory, original, new(1)), NativeBattlePackedMatrix32.FromMatrix(Matrix, null));
    }

    private static uint Pointer(NativeBattleDeathFrame frame)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(frame.Effect.Binding.Effect);
        return U32(profile.RequireState(frame.Memory).Module, profile.ModuleOffset(0x2742864));
    }

    private static NativeBattleDeathFrame WithPointer(NativeBattleDeathFrame frame, uint value)
    {
        NativeBattleDeathFamilyProfile profile = NativeBattleDeathFamilyProfile.ForEffect(frame.Effect.Binding.Effect);
        NativeBattleDeathState state = profile.RequireState(frame.Memory);
        return frame with { Memory = profile.WithState(frame.Memory, state with { Module = state.Module.Write(profile.ModuleOffset(0x2742864), BitConverter.GetBytes(value)) }) };
    }

    private static NativeBattleEffectArena Context(NativeBattleDeathFrame frame) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectNodeContextMemory.Capture(frame.Memory), NativeBattleActorWaveMesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), 0, 104);
    private static uint U32(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));

    internal static ImmutableArray<byte> Mesh(bool reverse = false)
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
