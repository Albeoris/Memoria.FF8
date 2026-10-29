using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRemainingStatusPresentationTests
{
    [Fact]
    public void Berserk_ray_capture_preserves_packets_and_retains_preprojection_centers()
    {
        byte[] ray = new byte[48];
        W(ray, 2, 1024);
        W(ray, 34, 1600);
        W(ray, 40, 160);
        NativeBattleActionMatrix matrix = Identity() with { Z = 4096 };
        NativeBattleCameraProjection projection = new(Identity(), 256, 160, 108);
        NativeBattleModuleProjectedVertex Project(NativeBattleVector point) => NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
        NativeBattlePilotVisualIdentity identity = new(108, 0x2521B08, 0, 42, 3);
        NativeBattleGfGeometryBatch plain = NativeBattleBerserk108Geometry.RayPackets(ray.ToImmutableArray(), Project);
        NativeBattleGfGeometryBatch captured = NativeBattleBerserk108Geometry.RayPackets(ray.ToImmutableArray(), Project, identity, matrix, projection);
        Assert.Equal(64, captured.AllocationBytes);
        AssertPacketParity(plain, captured);
        NativeBattlePilotVisualGeometry triangle = captured.Attempts[0].Primitive!.PilotGeometry!;
        Assert.Equal(new Vector3(0, 0, 4096), triangle.A);
        Assert.Equal(42, triangle.Identity.Generation);
        Assert.Equal(3, triangle.CornerCount);
        Assert.Equal(4, captured.Attempts[1].Primitive!.PilotGeometry!.CornerCount);
        Assert.All(captured.Attempts, attempt =>
        {
            NativeBattlePilotVisualGeometry geometry = attempt.Primitive!.PilotGeometry!;
            ImmutableArray<uint> words = attempt.Primitive.Words;
            for (int corner = 0; corner < geometry.CornerCount; corner++)
            {
                Vector3 point = NativeBattlePilotVisualGeometry.Project(geometry.Corner(corner), projection);
                Vector2 screen = new(point.X, point.Y);
                screen += geometry.ScreenOffsets[corner];
                uint packed = words[2 + corner * 2];
                Assert.Equal((float)unchecked((short)packed), screen.X);
                Assert.Equal((float)unchecked((short)(packed >> 16)), screen.Y);
            }
        });
    }

    [Fact]
    public void Sleep_zzz_reuse_changes_only_the_reborn_rows_presentation_generation()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24AA828, 0x24AA818, 16, 1);
        (pool, int slot) = pool.Append(0x61AA80);
        NativeBattleModulePoolTask root = pool.Slots[slot];
        NativeBattleSleep145Presentation.Allocated(root, 0);
        NativeBattleSleep145Presentation.Allocated(root, 1);
        NativeBattlePilotVisualIdentity first = NativeBattleSleep145Presentation.Identity(root, 0);
        NativeBattlePilotVisualIdentity other = NativeBattleSleep145Presentation.Identity(root, 1);
        NativeBattleSleep145Presentation.Allocated(root, 0);
        NativeBattlePilotVisualIdentity reused = NativeBattleSleep145Presentation.Identity(root, 0);
        Assert.Equal(first.Generation, reused.Generation);
        Assert.NotEqual(first.ResourceGeneration, reused.ResourceGeneration);
        Assert.Equal(other, NativeBattleSleep145Presentation.Identity(root, 1));
        Assert.Equal(0, root.ReadWord(12));
    }

    [Fact]
    public void Sleep_tint_capture_preserves_partial_writes_and_distinguishes_face_layers()
    {
        byte[] vertices = new byte[3 * 8];
        W(vertices, 8, 100);
        W(vertices, 18, 100);
        byte[] face = new byte[16];
        W(face, 2, 1);
        W(face, 4, 2);
        ImmutableArray<NativeBattleRawPolygon> polygons = [new(false, face.ToImmutableArray())];
        NativeBattleActionMatrix view = Identity() with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        bool Factors(IReadOnlyList<NativeBattleVector> positions, short[] factors)
        {
            Array.Fill(factors, (short)2048);
            return true;
        }
        NativeBattleGfGeometryBatch plain = NativeBattleActorTintPackets.Emit(polygons, vertices.ToImmutableArray(), view, projection, 0x808080, 0x08B0A0, colors, Factors);
        NativeBattleGfGeometryBatch captured = NativeBattleActorTintPackets.Emit(polygons, vertices.ToImmutableArray(), view, projection, 0x808080, 0x08B0A0, colors, Factors, pilotIdentity: new(145, 0x24AAAC0, 3, 7, 32));
        Assert.Equal(2, captured.Attempts.Length);
        AssertPacketParity(plain, captured);
        NativeBattlePilotVisualGeometry first = captured.Attempts[0].Primitive!.PilotGeometry!;
        NativeBattlePilotVisualGeometry tint = captured.Attempts[1].Primitive!.PilotGeometry!;
        Assert.Equal(new Vector3(100, 0, 0), first.B);
        Assert.Equal(first.A, tint.A);
        Assert.NotEqual(first.Identity.Primitive, tint.Identity.Primitive);
    }

    private static void AssertPacketParity(NativeBattleGfGeometryBatch plain, NativeBattleGfGeometryBatch captured)
    {
        Assert.Equal(plain.AllocationBytes, captured.AllocationBytes);
        Assert.Equal(plain.Attempts.Length, captured.Attempts.Length);
        for (int index = 0; index < plain.Attempts.Length; index++)
        {
            Assert.Equal(plain.Attempts[index].Data.ToArray(), captured.Attempts[index].Data.ToArray());
            Assert.Equal(plain.Attempts[index].WriteMask.ToArray(), captured.Attempts[index].WriteMask.ToArray());
            Assert.Equal(plain.Attempts[index].ByteOffsetRelative, captured.Attempts[index].ByteOffsetRelative);
            Assert.Equal(plain.Attempts[index].Primitive?.Bucket, captured.Attempts[index].Primitive?.Bucket);
        }
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static void W(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
}
