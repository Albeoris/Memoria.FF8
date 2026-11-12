using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>An original FFVIII pose and its placement in the host arena's world space.</summary>
public sealed record CrossoverFf8BattleActorVisual(BattleModel Body, BattleModel? Weapon, NativeBattleGraphicsModel Model, Matrix4x4 OriginToHost)
{
    public NativeBattleModelTextureBinding? BodyTextures { get; init; }
    public NativeBattleEffectTextureState? Textures { get; init; }
    public NativeBattleActionMatrix? DetachedWeapon { get; init; }
}

public static class CrossoverFf8BattleActorRenderer
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    public static void Draw(ITriangleRenderer renderer, CrossoverFf8BattleActorVisual visual, Func<Vector3, Vector3> cameraTransform, int width, int height, float focal)
    {
        Dictionary<(ushort Page, ushort Clut), PixelImage> pages = [];
        if ((visual.BodyTextures is null) != (visual.Textures is null))
        {
            throw new InvalidDataException("Mixed source texture animation requires both its model binding and VRAM owner.");
        }
        if ((visual.Model.Flags0 & 6) != 2 || ((visual.Model.Flags2 ?? throw new InvalidDataException("Mixed actor rendering requires native model flags.")) & 0x84) != 0)
        {
            return;
        }
        VisitGeometry(visual, DrawMesh);

        void DrawMesh(BattleMesh mesh, IReadOnlyList<PixelImage> textures, ImmutableArray<NativeBattleActionMatrix> bones, bool bodyOwner)
        {
            Vector3[] cameraPositions = WorldVertices(visual, mesh, bones);
            for (int index = 0; index < cameraPositions.Length; index++)
            {
                cameraPositions[index] = cameraTransform(cameraPositions[index]);
            }
            foreach (BattleFace face in mesh.Faces)
            {
                NativeBattleFaceMaterial? nativeMaterial = bodyOwner && visual.BodyTextures is { } binding ? NativeBattleFaceMaterialBinding.Apply507550(face.NativeMaterial ?? throw new InvalidDataException("Mixed source texture animation requires original polygon materials."), binding) : null;
                PixelImage texture = textures[face.Texture];
                if (nativeMaterial is not null && !pages.TryGetValue((nativeMaterial.TexturePage, nativeMaterial.Clut), out texture!))
                {
                    texture = NativeBattleEffectTextureReader.DecodePcRegion(visual.Textures!, nativeMaterial.TexturePage, nativeMaterial.Clut, 0, 0, 256, 256);
                    pages.Add((nativeMaterial.TexturePage, nativeMaterial.Clut), texture);
                }
                Vector2 textureSize = new(texture.Width, texture.Height);
                Vector4 tint = new(face.Color.R / 128f, face.Color.G / 128f, face.Color.B / 128f, face.Color.A / 255f);
                TriangleMaterial material = new(texture, TriangleBlend.Replace, TriangleDepthComparison.GreaterEqual, true, 1f / 255, TriangleSampler.PointClamp) { PreserveTargetAlpha = false, TextureSelectsAlpha = false };
                CameraSpaceVertex Vertex(int index) => new(cameraPositions[face.Vertices[index]], (nativeMaterial?.Uv ?? face.Uv)[index] / textureSize, tint * (face.VertexColors?[index] ?? Vector4.One));

                CameraSpaceVertex a = Vertex(0);
                CameraSpaceVertex b = Vertex(1);
                CameraSpaceVertex c = Vertex(2);
                if (face.Vertices.Length == 4)
                {
                    CameraSpaceVertex d = Vertex(3);
                    CameraSpaceTriangleSubmission.Draw(renderer, a, b, d, width, height, focal, material, cull: true, clockwiseFront: true);
                    CameraSpaceTriangleSubmission.Draw(renderer, d, c, a, width, height, focal, material, cull: true, clockwiseFront: true);
                }
                else
                {
                    CameraSpaceTriangleSubmission.Draw(renderer, a, b, c, width, height, focal, material, cull: true, clockwiseFront: true);
                }
            }
        }
    }

    public static Ff7BattleActorGeometry CaptureActorGeometry(CrossoverFf8BattleActorVisual visual)
    {
        ImmutableArray<(Vector3 A, Vector3 B, Vector3 C)>.Builder faces = ImmutableArray.CreateBuilder<(Vector3 A, Vector3 B, Vector3 C)>();
        ImmutableArray<NativeBattleForeignSurface>.Builder surfaces = ImmutableArray.CreateBuilder<NativeBattleForeignSurface>();
        Dictionary<(ushort Page, ushort Clut), PixelImage> pages = [];
        ImmutableArray<Vector3>.Builder skeleton = ImmutableArray.CreateBuilder<Vector3>();
        ImmutableArray<(Vector3 Start, Vector3 End)>.Builder segments = ImmutableArray.CreateBuilder<(Vector3 Start, Vector3 End)>();
        NativeBattleGraphicsModel model = visual.Model;
        NativeBattlePoseSource source = visual.Body.NativePoseSource ?? throw new InvalidDataException("Mixed actor geometry requires the original body skeleton.");
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new InvalidDataException("Mixed actor geometry requires the published native world matrix.");
        Vector3 root = Transform(visual, world, default);
        skeleton.Add(root);
        foreach (NativeBattleActionMatrix bone in NativeBattleBodyBoneMatrices.Require(model, source))
        {
            skeleton.Add(Transform(visual, NativeBattleGfMatrices.Compose56C2F0(world, bone), default));
        }
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            NativeBattleVector first = NativeBattlePoseMatrices.ReadBone(model, source, checked((byte)bone), 0);
            NativeBattleVector second = NativeBattlePoseMatrices.ReadBone(model, source, checked((byte)bone), 4096);
            segments.Add((Vector3.Transform(new(first.X, first.Y, first.Z), visual.OriginToHost), Vector3.Transform(new(second.X, second.Y, second.Z), visual.OriginToHost)));
        }
        VisitGeometry(visual, (mesh, textures, bones, bodyOwner) =>
        {
            Vector3[] vertices = WorldVertices(visual, mesh, bones);
            for (int index = 0; index < mesh.Faces.Count; index++)
            {
                BattleFace face = mesh.Faces[index];
                Vector3 a = vertices[face.Vertices[0]];
                Vector3 b = vertices[face.Vertices[1]];
                Vector3 c = vertices[face.Vertices[2]];
                NativeBattleFaceMaterial? nativeMaterial = bodyOwner && visual.BodyTextures is { } binding ? NativeBattleFaceMaterialBinding.Apply507550(face.NativeMaterial ?? throw new InvalidDataException("Mixed actor geometry requires its original material."), binding) : null;
                PixelImage texture = textures[face.Texture];
                if (nativeMaterial is not null && !pages.TryGetValue((nativeMaterial.TexturePage, nativeMaterial.Clut), out texture!))
                {
                    texture = NativeBattleEffectTextureReader.DecodePcRegion(visual.Textures ?? throw new InvalidDataException("Mixed actor geometry requires its texture owner."), nativeMaterial.TexturePage, nativeMaterial.Clut, 0, 0, 256, 256);
                    pages.Add((nativeMaterial.TexturePage, nativeMaterial.Clut), texture);
                }
                Vector2 textureSize = new(texture.Width, texture.Height);
                Vector4 tint = new(face.Color.R / 128f, face.Color.G / 128f, face.Color.B / 128f, face.Color.A / 255f);
                NativeBattleForeignRenderMaterial material = new(new(texture, TriangleBlend.Replace, TriangleDepthComparison.GreaterEqual, true, 1f / 255, TriangleSampler.PointClamp) { PreserveTargetAlpha = false, TextureSelectsAlpha = false }, true);
                Vector2 Uv(int corner) => (nativeMaterial?.Uv ?? face.Uv)[corner] / textureSize;
                Vector4 Color(int corner) => tint * (face.VertexColors?[corner] ?? Vector4.One);
                NativeBattleForeignSurface Surface(int first, int second, int third) => new(material, [Uv(first), Uv(second), Uv(third)], [Color(first), Color(second), Color(third)]);
                if (face.Vertices.Length == 4)
                {
                    Vector3 d = vertices[face.Vertices[3]];
                    faces.Add((a, b, d));
                    faces.Add((d, c, a));
                    surfaces.Add(Surface(0, 1, 3));
                    surfaces.Add(Surface(3, 2, 0));
                }
                else
                {
                    faces.Add((a, b, c));
                    surfaces.Add(Surface(0, 1, 2));
                }
            }
        });
        return new([], skeleton.ToImmutable())
        {
            GuestRoot = root,
            GuestEffectAnchor = EffectAnchor(visual),
            GuestFaces = faces.ToImmutable(),
            GuestSurfaces = surfaces.ToImmutable(),
            Bones = segments.ToImmutable()
        };
    }

    public static Vector3 EffectAnchor(CrossoverFf8BattleActorVisual visual)
    {
        NativeBattlePoseSource source = visual.Body.NativePoseSource ?? throw new InvalidDataException("Mixed actor geometry requires the original body skeleton.");
        NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(visual.Model, source);
        return Vector3.Transform(new(anchor.X, anchor.Y, anchor.Z), visual.OriginToHost);
    }

    private static void VisitGeometry(CrossoverFf8BattleActorVisual visual, Action<BattleMesh, IReadOnlyList<PixelImage>, ImmutableArray<NativeBattleActionMatrix>, bool> visit)
    {
        NativeBattleGraphicsModel model = visual.Model;
        if (model is not { ModelId4: < 11 } || visual.Body.EditableOverride is not null || visual.Weapon?.EditableOverride is not null)
        {
            throw new NotSupportedException("Mixed actor rendering requires original FFVIII party geometry.");
        }
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new InvalidDataException("Mixed actor rendering requires the published native world matrix.");
        NativeBattlePoseSource bodySource = visual.Body.NativePoseSource ?? throw new InvalidDataException("Mixed actor rendering requires the original body skeleton.");
        ImmutableArray<NativeBattleActionMatrix> bodyBones = NativeBattleBodyBoneMatrices.Require(model, bodySource).Select(bone => NativeBattleGfMatrices.Compose56C2F0(world, bone)).ToImmutableArray();
        uint bodyMask = model.SequenceState?.MeshMask124 ?? throw new InvalidDataException("Mixed actor rendering requires the native object mask.");
        IReadOnlyList<BattleMesh> bodyMeshes = visual.Body.NativeControlMeshes ?? visual.Body.Meshes;
        BattleModel? weapon = visual.Weapon;
        IReadOnlyList<BattleMesh> weaponMeshes = weapon?.NativeControlMeshes ?? weapon?.Meshes ?? [];
        bool sharedSkeleton = weapon is not null && (weapon.NativeWeaponUsesBodySkeleton ?? throw new InvalidDataException("Mixed actor rendering requires the original weapon skeleton binding."));
        if (sharedSkeleton && (bodyMeshes.Count < 2 || weaponMeshes.Count != 1))
        {
            throw new InvalidDataException("A shared-skeleton weapon must replace body object 1 with its original single object.");
        }
        for (int objectIndex = 0; objectIndex < bodyMeshes.Count; objectIndex++)
        {
            if ((bodyMask & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            bool weaponObject = sharedSkeleton && objectIndex == 1;
            visit(weaponObject ? weaponMeshes[0] : bodyMeshes[objectIndex], weaponObject ? weapon!.Textures : visual.Body.Textures, bodyBones, !weaponObject);
        }
        if (weapon is not null && !sharedSkeleton && (visual.DetachedWeapon is not null || (model.Flags0 & 0x200) == 0))
        {
            NativeBattlePoseSource weaponSource = weapon.NativePoseSource ?? throw new InvalidDataException("Mixed actor rendering requires the original linked weapon skeleton.");
            ImmutableArray<NativeBattleActionMatrix> weaponBones = visual.DetachedWeapon is { } detached ? Enumerable.Repeat(detached, weaponSource.BoneCount).ToImmutableArray() : NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, weaponSource, Identity);
            foreach (BattleMesh mesh in weaponMeshes)
            {
                visit(mesh, weapon.Textures, weaponBones, false);
            }
        }
    }

    private static Vector3[] WorldVertices(CrossoverFf8BattleActorVisual visual, BattleMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones)
    {
        Vector3[] positions = new Vector3[mesh.Vertices.Count];
        for (int index = 0; index < positions.Length; index++)
        {
            BattleVertex vertex = mesh.Vertices[index];
            NativeBattleVector position = vertex.NativePosition ?? throw new InvalidDataException("Mixed actor rendering requires the original model vertex WORDs.");
            positions[index] = Transform(visual, bones[vertex.Bone], position);
        }
        return positions;
    }

    private static Vector3 Transform(CrossoverFf8BattleActorVisual visual, NativeBattleActionMatrix matrix, NativeBattleVector position)
    {
        NativeBattleFullMacVertex posed = NativeBattleGfMatrices.TransformMac460840(matrix, position);
        return Vector3.Transform(new(posed.X, posed.Y, posed.Z), visual.OriginToHost);
    }
}
