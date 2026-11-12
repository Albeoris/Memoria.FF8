using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>The host owns this material; it is never a fabricated native resource or VRAM address.</summary>
public interface INativeBattleForeignMaterial
{
}

public sealed record NativeBattleForeignSurface(INativeBattleForeignMaterial Material, ImmutableArray<Vector2> Uv, ImmutableArray<Vector4> Colors);
public sealed record NativeBattleForeignModelFace(NativeBattleVector A, NativeBattleVector B, NativeBattleVector C, NativeBattleForeignSurface Surface)
{
    /// <summary>Call-local packed polygon input for shared native triangle consumers, without resource publication.</summary>
    public NativeBattleRawMesh Triangle()
    {
        byte[] bytes = new byte[16];
        Put(0, 0);
        Put(2, 1);
        Put(4, 2);
        Put(8, Uv(0));
        Put(12, Uv(1));
        Put(6, Uv(2));
        return new([new(A, 0), new(B, 0), new(C, 0)], [new(false, bytes.ToImmutableArray())]);

        void Put(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        ushort Uv(int corner)
        {
            Vector2 uv = Surface.Uv[corner];
            return (ushort)(unchecked((byte)NativePrecision53.RoundInteger(uv.X * 255)) | unchecked((byte)NativePrecision53.RoundInteger(uv.Y * 255)) << 8);
        }
    }
}

/// <summary>Already posed host geometry in source-local coordinates, including any host weapon parts.</summary>
public sealed record NativeBattleForeignModelGeometry(NativeBattleActionMatrix World, NativeBattleEffectActorGeometry Anchors, ImmutableArray<NativeBattleForeignModelFace> Faces)
{
    public static NativeBattleActionMatrix Identity { get; } = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    public NativeBattleGeometryPrimitive BindSurface(NativeBattleGeometryPrimitive primitive, NativeBattleForeignModelFace face, NativeBattlePilotVisualIdentity identity)
    {
        Vector3 Point(NativeBattleVector point) => NativeBattlePilotVisualGeometry.Transform(World, point);
        return primitive with
        {
            PilotGeometry = new(identity, Point(face.A), Point(face.B), Point(face.C), default, 3) { ForeignSurface = face.Surface }
        };
    }
}
