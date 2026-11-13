using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed record CrossoverNativeStageColor(NativeBattleSummonColors Colors, short Depth, uint TargetRgb = 0)
{
    public uint Apply(uint argb)
    {
        uint native = SwapRedBlue(argb);
        return SwapRedBlue(Colors.Interpolate(native, Depth, TargetRgb));
    }

    private static uint SwapRedBlue(uint color) => (color & 0xFF00FF00u) | ((color & 0x00FF0000u) >> 16) | ((color & 0x000000FFu) << 16);
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
    private const int CameraWidth = 320;
    private const int CameraHeight = 240;
    private const int NativeDrawHeight = 216;
    private BattleRenderer? native;
    public Func<INativeBattleForeignMaterial, NativeBattleForeignRenderMaterial>? ForeignMaterialResolver { get; set; }

    public static NativeBattleCameraProjection Camera(SceneCameraPlacement host, Matrix4x4 originToHost)
    {
        if (!Matrix4x4.Invert(originToHost, out Matrix4x4 inverse))
        {
            throw new InvalidDataException("Mixed module projection requires an invertible origin placement.");
        }
        BattleCameraBasis basis = BattleCameraBasis.Create(host);
        Vector3 eye = Vector3.Transform(basis.Position, inverse);
        Vector3 forward = Vector3.Normalize(Vector3.TransformNormal(basis.Forward, inverse));
        Vector3 right = Vector3.Normalize(Vector3.TransformNormal(basis.Right, inverse));
        Vector3 down = -Vector3.Normalize(Vector3.TransformNormal(basis.Up, inverse));
        ImmutableArray<short> words = [Q(right.X), Q(right.Y), Q(right.Z), Q(down.X), Q(down.Y), Q(down.Z), Q(forward.X), Q(forward.Y), Q(forward.Z)];
        NativeBattleActionMatrix view = new(words, NativePrecision53.RoundInteger(-Vector3.Dot(right, eye)), NativePrecision53.RoundInteger(-Vector3.Dot(down, eye)), NativePrecision53.RoundInteger(-Vector3.Dot(forward, eye)), 4096);
        return new(view, host.FocalLength ?? 512, CameraWidth / 2, CameraHeight / 2);

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
        native ??= new(CameraWidth, CameraHeight, new(0, 0, CameraWidth, NativeDrawHeight));
        native.ForeignMaterialResolver = ForeignMaterialResolver;
        native.TriangleRenderer = new HostPass(renderer, width, height, originToHostScale);
        native.RenderNativePrimitiveFrame(frame, source, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), textureWindowCache: executed.Cache);
        return native.DrawnTriangles;
    }

    private sealed class HostPass(ITriangleRenderer host, int width, int height, float originToHostScale) : ITriangleRenderer
    {
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void End()
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            TriangleMaterial mapped = material with { DepthComparison = material.DepthComparison == TriangleDepthComparison.Always ? TriangleDepthComparison.Always : TriangleDepthComparison.GreaterEqual };
            host.Draw(Map(a), Map(b), Map(c), mapped);
        }

        private TriangleVertex Map(TriangleVertex source)
        {
            if (source.ReciprocalW == 1)
            {
                Vector2 screen = source.Position * new Vector2(width / (float)CameraWidth, height / (float)CameraHeight);
                return source with { Position = screen, Depth = 1 / originToHostScale };
            }
            // Match the host's centered camera and its height-based focal length on both axes.
            Vector2 position = (source.Position - new Vector2(CameraWidth / 2f, CameraHeight / 2f)) * (height / (float)CameraHeight) + new Vector2(width / 2f, height / 2f);
            return source with { Position = position, Depth = source.ReciprocalW / originToHostScale };
        }
    }
}
