using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematic31CaptureTests
{
    [Theory]
    [InlineData((ushort)265)]
    [InlineData((ushort)266)]
    [InlineData((ushort)267)]
    [InlineData((ushort)268)]
    [InlineData((ushort)269)]
    [InlineData((ushort)270)]
    public void Selected_model_resource_retains_detached_native_output(ushort effect)
    {
        new NativeBattleCinematicModelTests().Selected_model_resource_capture_preserves_actual_geometry_pointer_and_all_native_output(effect);
    }

    [Theory]
    [InlineData((ushort)265)]
    [InlineData((ushort)266)]
    [InlineData((ushort)267)]
    [InlineData((ushort)268)]
    [InlineData((ushort)269)]
    [InlineData((ushort)270)]
    public void Selected_particle_resource_change_retains_actual_birth_and_detached_native_output(ushort effect)
    {
        new NativeBattleWaterParticleRectangleTests().Selected_resource_changes_at_one_particle_birth_preserve_detached_native_state_and_previous_geometry(effect);
    }

    [Theory]
    [InlineData((ushort)265)]
    [InlineData((ushort)266)]
    [InlineData((ushort)267)]
    [InlineData((ushort)268)]
    [InlineData((ushort)269)]
    [InlineData((ushort)270)]
    public void Fixed_screen_overlay_retains_discrete_policy_and_native_stores(ushort effect)
    {
        new NativeBattleCinematicOverlayTests().Selected_fixed_overlay_keeps_native_discrete_screen_operation_and_zero_depth(effect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dissolve_captures_only_accepted_deformed_corners_and_already_projected_depths_without_native_changes(bool quad)
    {
        ImmutableArray<byte> face = Face(quad);
        List<int> reads = [];
        NativeBattleCinematicActorDissolve52Vertex Vertex(int index)
        {
            reads.Add(index);
            uint screen = index switch { 0 => 0u, 1 => 20u, 2 => 20u << 16, _ => 20u | 20u << 16 };
            return new(screen, checked((ushort)(900 + index * 123)), 0);
        }
        NativeBattleCinematicActorDissolve52Emission native = NativeBattleCinematicActorDissolve52.EmitFace(face, quad, 255, 0, Vertex, _ => throw new InvalidOperationException(), 0x1000);
        string frozen = NativeBattleTestSnapshots.Json(native);
        int[] nativeReads = reads.ToArray();
        reads.Clear();
        List<int> worldReads = [];
        NativeBattlePilotVisualIdentity identity = new(265, 11, 2, 3, 4) { ResourceGeneration = 55 };
        NativeBattleCinematicActorDissolve52Emission captured = NativeBattleCinematicActorDissolve52.EmitFace(face, quad, 255, 0, Vertex, _ => throw new InvalidOperationException(), 0x1000, identity, index =>
        {
            worldReads.Add(index);
            return new Vector3(index * 10, -index, 500 + index);
        });
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(nativeReads, reads);
        Assert.Equal(nativeReads, worldReads);
        NativeBattlePilotVisualGeometry geometry = Assert.Single(captured.Packets.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(identity, geometry.Identity);
        Assert.Equal(new Vector3(0, 0, 500), geometry.A);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(new Vector4(900, 1023, 1146, quad ? 1269 : 0), geometry.SourceDepths);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)255)]
    public void Dead_or_rejected_dissolve_does_not_read_presentation_corners(byte counter)
    {
        int nativeReads = 0;
        NativeBattleCinematicActorDissolve52Emission result = NativeBattleCinematicActorDissolve52.EmitFace(Face(true), true, counter, 0, index =>
        {
            nativeReads++;
            uint screen = index switch { 0 => 0u, 1 => 20u << 16, 2 => 20u, _ => 20u | 20u << 16 };
            return new(screen, 900, 0);
        }, _ => throw new InvalidOperationException(), 0x1000, new(265, 11, 2, 3, 4), _ => throw new InvalidOperationException("Rejected presentation corner was read."));
        Assert.Equal(counter == 0 ? 0 : 4, nativeReads);
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.All(result.Packets.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    [Fact]
    public void Dissolve_clone_birth_and_real_loaded_resource_change_identity_without_changing_native_arena()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(64, new byte[] { 4, 3, 2, 1 });
        NativeBattlePilotVisualIdentity parent = new(265, 11, 2, 3, 4) { ResourceGeneration = 55 };
        string frozen = NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(arena));
        NativeBattlePilotVisualLifetimes.Allocated(arena, 0x20E1000, 0);
        NativeBattlePilotVisualIdentity first = NativeBattleCinematic31Presentation.DissolveSource(parent, arena, 0x20E1000, 0x1D9B000, 0x1920000);
        NativeBattlePilotVisualIdentity replacedResource = NativeBattleCinematic31Presentation.DissolveSource(parent, arena, 0x20E1000, 0x1D9B000, 0x1930000);
        Assert.NotEqual(first.ResourceGeneration, replacedResource.ResourceGeneration);
        NativeBattlePilotVisualLifetimes.Allocated(arena, 0x20E1000, 0);
        NativeBattlePilotVisualIdentity reusedClone = NativeBattleCinematic31Presentation.DissolveSource(parent, arena, 0x20E1000, 0x1D9B000, 0x1920000);
        Assert.NotEqual(first.ResourceGeneration, reusedClone.ResourceGeneration);
        Assert.Equal(parent.Bank, reusedClone.Bank);
        Assert.Equal(parent.Slot, reusedClone.Slot);
        Assert.Equal(parent.Generation, reusedClone.Generation);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(arena)));
    }

    private static ImmutableArray<byte> Face(bool quad)
    {
        byte[] bytes = new byte[quad ? 20 : 16];
        for (int corner = 0; corner < (quad ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(corner * 2), checked((ushort)corner));
        }
        return bytes.ToImmutableArray();
    }
}
