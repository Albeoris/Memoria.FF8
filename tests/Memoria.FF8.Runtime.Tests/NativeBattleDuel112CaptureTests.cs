using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112CaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hierarchy_selected_and_morphed_faces_keep_native_W_scratch_and_packet_writes(bool morph)
    {
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 100, 160, 108);
        NativeBattlePilotVisualIdentity identity = new(112, 0x2521950, 0, 42, 180) { ResourceGeneration = NativeBattleDuel112Resources.Address + 0x100 };
        NativeBattleDuel112HierarchyStep Run(bool capture)
        {
            byte[] image = new byte[NativeBattleDuel112Resources.EndAddress - NativeBattleDuel112Resources.Address];
            Source(4).Data.AsSpan().CopyTo(image.AsSpan(0x100));
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x500), 0x30);
            NativeBattleDuel112Resources resources = new(image.ToImmutableArray(), [], [], []);
            NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x4000, Enumerable.Repeat((byte)0xA5, 64).ToArray()), Duel112 = new(true, parameters, resources) };
            NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0);
            NativeBattleDuel112Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), new(123));
            NativeBattleGfAnimationSource source = new(NativeBattleDuel112Resources.Address + 0x100, Source(4).Data);
            return NativeBattleDuel112Hierarchy.Draw6CD280(frame, source, Node(morph), new(NativeBattleDuel112Resources.Address + 0x500, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000, new(0, 0, 2000), null, null), view, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, capture ? identity : null);
        }
        NativeBattleDuel112HierarchyStep native = Run(false);
        string detachedNative = NativeBattleTestSnapshots.Json(native);
        NativeBattleDuel112HierarchyStep captured = Run(true);
        Assert.Equal(detachedNative, NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(identity.Generation, visual.Identity.Generation);
        Assert.Equal(identity.ResourceGeneration, visual.Identity.ResourceGeneration);
        Assert.Equal(new Vector3(morph ? -350 : -400, -400, 2000), visual.A);
        Assert.Equal(new Vector3(morph ? 450 : 400, 400, 2000), visual.D);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, captured.Frame.Memory.Arena!.Read(0x4000 + vertex * 8 + 6, 2));
        }
    }

    [Fact]
    public void Fixed_flashes_capture_actual_screen_corners_discretely_and_preserve_mode_packets()
    {
        NativeBattleModulePacketBatch native = NativeBattleDuelSharedPackets.Flash6CBF90();
        string detached = NativeBattleTestSnapshots.Json(native);
        NativeBattlePilotVisualIdentity identity = new(112, 0x2521950, 0, 43, 0x6CBF90);
        NativeBattleModulePacketBatch captured = NativeBattleDuel112Target.CaptureFixedFlash(native, identity);
        Assert.Equal(detached, NativeBattleTestSnapshots.Json(captured));
        Assert.Null(captured.Primitives[0].Primitive.PilotGeometry);
        Assert.Null(captured.Primitives[2].Primitive.PilotGeometry);
        NativeBattlePilotVisualGeometry flash = Assert.IsType<NativeBattlePilotVisualGeometry>(captured.Primitives[1].Primitive.PilotGeometry);
        Assert.Equal(new Vector3(320, 216, 0), flash.D);
        Assert.True(flash.ScreenSpace);
        Assert.False(flash.Continuous);
        Assert.Empty(NativeBattleDuel112Target.CaptureFixedFlash(NativeBattleDuelSharedPackets.Flash6A83B0(40, 55, 50, 3, 3), identity).Primitives);
    }

    private static NativeBattleGfAnimationNode Node(bool interpolate = false)
    {
        byte[] node = new byte[44];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        if (interpolate)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        }
        return new(0, 0, node.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int count)
    {
        int faces = 12 + count * 16;
        byte[] bytes = new byte[16 + faces + 68];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        short[] coordinates = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int at = 28 + (shape * count + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), (short)((vertex < 4 ? coordinates[vertex * 2] : 0) + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), (short)(vertex < 4 ? coordinates[vertex * 2 + 1] : 0));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int group = 16 + faces + 28;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(group + 4), 0x3C332211);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(group + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x1000224, bytes.ToImmutableArray());
    }

}
