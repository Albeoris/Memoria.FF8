using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed record CrossoverNativeStageColor(NativeBattleSummonColors Colors, short Depth)
{
    public uint Apply(uint argb) => Colors.Interpolate(argb, Depth, 0);
}

public sealed record CrossoverNativeEffectVisual(CrossoverNativeSpriteRenderer Renderer, ImmutableArray<NativeBattleModuleDraw> Draws, NativeBattleEffectTextureState Textures, Matrix4x4 OriginToHost, float Scale)
{
    public CrossoverNativeStageColor? StageColor { get; init; }
    public NativeBattleLogicalOrdering? Ordering { get; init; }
    public NativeBattleEffectMemory? Memory { get; init; }

    public int Draw(ITriangleRenderer renderer, SceneCameraPlacement camera, int width, int height)
    {
        int triangles = 0;
        if (Ordering is not null && Memory is { } memory)
        {
            triangles += Renderer.DrawOrdering(renderer, Ordering, memory.Textures!, width, height, Scale, (reference, count) => reference.Bank == "20DFAB8" ? memory.Arena!.Read(reference.ByteOffset, count) : (memory.PrimitiveProcess! with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count));
        }
        return triangles + Renderer.Draw(renderer, Draws, Textures, CrossoverNativeSpriteRenderer.Camera(camera, OriginToHost), width, height, Scale);
    }
}

/// <summary>Original module sprite packets composed with the encounter camera and depth buffer.</summary>
public sealed class CrossoverNativeSpriteRenderer(NativeBattlePrimitiveTables tables, ImmutableArray<NativeBattleStaticMeshResource> meshes, NativeBattleSummonColors colors)
{
    private BattleRenderer? native;

    public static NativeBattleCameraProjection Camera(SceneCameraPlacement host, Matrix4x4 originToHost)
    {
        if (!Matrix4x4.Invert(originToHost, out Matrix4x4 inverse))
        {
            throw new InvalidDataException("Mixed module projection requires an invertible origin placement.");
        }
        Vector3 eye = Vector3.Transform(host.Position, inverse);
        Vector3 forward;
        Vector3 right;
        Vector3 down;
        if (host.Basis is { } basis)
        {
            forward = Vector3.Normalize(Vector3.TransformNormal(basis.Forward, inverse));
            right = Vector3.Normalize(Vector3.TransformNormal(basis.Right, inverse));
            down = -Vector3.Normalize(Vector3.TransformNormal(basis.Up, inverse));
        }
        else
        {
            Vector3 focus = Vector3.Transform(host.Focus, inverse);
            forward = Vector3.Normalize(focus - eye);
            right = Vector3.Normalize(Vector3.Cross(forward, -Vector3.UnitY));
            down = -Vector3.Cross(right, forward);
        }
        ImmutableArray<short> words = [Q(right.X), Q(right.Y), Q(right.Z), Q(down.X), Q(down.Y), Q(down.Z), Q(forward.X), Q(forward.Y), Q(forward.Z)];
        NativeBattleActionMatrix view = new(words, NativePrecision53.RoundInteger(-Vector3.Dot(right, eye)), NativePrecision53.RoundInteger(-Vector3.Dot(down, eye)), NativePrecision53.RoundInteger(-Vector3.Dot(forward, eye)), 4096);
        return new(view, host.FocalLength ?? 512, 160, 120);

        static short Q(float value) => checked((short)NativePrecision53.RoundInteger(value * 4096d));
    }

    public int Draw(ITriangleRenderer renderer, ImmutableArray<NativeBattleModuleDraw> draws, NativeBattleEffectTextureState source, NativeBattleCameraProjection camera, int width, int height, float originToHostScale)
    {
        if (width <= 0 || height <= 0 || !float.IsFinite(originToHostScale) || originToHostScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originToHostScale), "Mixed sprite composition requires positive render dimensions and a finite positive origin scale.");
        }
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        int cursor = 0;
        foreach (NativeBattleModuleDraw draw in draws)
        {
            if (draw.Kind == NativeBattleModuleDrawKind.Sound)
            {
                continue;
            }
            NativeBattleModulePacketBatch packets = draw.Kind switch
            {
                NativeBattleModuleDrawKind.Flash => NativeBattleFlashPackets.Emit(draw),
                NativeBattleModuleDrawKind.Sprite => NativeBattleModulePackets.EmitSprite571DC0(draw, camera, 0, 17, 2),
                NativeBattleModuleDrawKind.Mesh => NativeBattleModulePackets.EmitMesh572200(draw, meshes.Single(mesh => mesh.Id == draw.MeshId), camera, 17, 2, colors),
                _ => throw new NotSupportedException("Mixed module composition requires an admitted original sprite or mesh producer.")
            };
            foreach (NativeBattleAllocatedPrimitive allocated in packets.Primitives)
            {
                NativeBattleGeometryPrimitive primitive = allocated.Primitive;
                ordering = ordering.Prepend(primitive.Bucket, new("off_B6D070", 0, cursor + allocated.ByteOffset), primitive.Words, allocated.Flags20, primitive.VertexPayload4);
            }
            cursor += packets.AllocationBytes;
        }
        return DrawOrdering(renderer, ordering, source, width, height, originToHostScale);
    }

    public int DrawOrdering(ITriangleRenderer renderer, NativeBattleLogicalOrdering ordering, NativeBattleEffectTextureState source, int width, int height, float originToHostScale, Func<NativeBattlePrimitiveReference, int, byte[]>? readPhysical = null)
    {
        if (width <= 0 || height <= 0 || !float.IsFinite(originToHostScale) || originToHostScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originToHostScale), "Mixed primitive composition requires positive render dimensions and a finite positive origin scale.");
        }
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null) with { ScaleU = 0, ScaleV = 0, SpriteTileCacheControl1CCFD90 = 0 };
        var executed = NativeBattleLogicalPasses.Execute(ordering, tables, new(0) { TextureWindowBits = 0 }, 1, cache, readPhysical);
        NativeBattlePreparedPrimitiveFrame frame = new(executed.Commands, [])
        {
            LastDescriptorPerspective = true,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        if (native is null || native.Width != width || native.Height != height)
        {
            native = new(width, height, new(0, 0, width, Math.Max(1, height * 216 / 240)));
        }
        native.TriangleRenderer = new HostPass(renderer, originToHostScale);
        native.RenderNativePrimitiveFrame(frame, source, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), textureWindowCache: executed.Cache);
        return native.DrawnTriangles;
    }

    private sealed class HostPass(ITriangleRenderer host, float scale) : ITriangleRenderer
    {
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void End()
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            TriangleMaterial mapped = material with { DepthComparison = TriangleDepthComparison.GreaterEqual };
            host.Draw(Map(a), Map(b), Map(c), mapped);
        }

        private TriangleVertex Map(TriangleVertex source) => source with { Depth = source.ReciprocalW / scale };
    }
}
