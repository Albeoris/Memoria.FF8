using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>An original FFVIII pose and its placement in the host arena's world space.</summary>
public sealed record CrossoverFf8BattleActorVisual(BattleModel Body, BattleModel Weapon, NativeBattleGraphicsModel Model, Matrix4x4 OriginToHost)
{
    public NativeBattleModelTextureBinding? BodyTextures { get; init; }
    public NativeBattleEffectTextureState? Textures { get; init; }
}

public static class CrossoverFf8BattleActorRenderer
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    public static void Draw(ITriangleRenderer renderer, CrossoverFf8BattleActorVisual visual, Func<Vector3, Vector3> cameraTransform, int width, int height, float focal)
    {
        NativeBattleGraphicsModel model = visual.Model;
        Dictionary<(ushort Page, ushort Clut), PixelImage> pages = [];
        if ((visual.BodyTextures is null) != (visual.Textures is null))
        {
            throw new InvalidDataException("Mixed source texture animation requires both its model binding and VRAM owner.");
        }
        if (model is not { ModelId4: 0 } || visual.Body.EditableOverride is not null || visual.Weapon.EditableOverride is not null)
        {
            throw new NotSupportedException("Mixed actor rendering requires original Squall body and weapon resources.");
        }
        if ((model.Flags0 & 6) != 2 || ((model.Flags2 ?? throw new InvalidDataException("Mixed actor rendering requires native model flags.")) & 0x84) != 0)
        {
            return;
        }
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new InvalidDataException("Mixed actor rendering requires the published native world matrix.");
        NativeBattlePoseSource bodySource = visual.Body.NativePoseSource ?? throw new InvalidDataException("Mixed actor rendering requires the original body skeleton.");
        ImmutableArray<NativeBattleActionMatrix> bodyBones = NativeBattleBodyBoneMatrices.Require(model, bodySource).Select(bone => NativeBattleGfMatrices.Compose56C2F0(world, bone)).ToImmutableArray();
        uint bodyMask = model.SequenceState?.MeshMask124 ?? throw new InvalidDataException("Mixed actor rendering requires the native object mask.");
        IReadOnlyList<BattleMesh> bodyMeshes = visual.Body.NativeControlMeshes ?? visual.Body.Meshes;
        IReadOnlyList<BattleMesh> weaponMeshes = visual.Weapon.NativeControlMeshes ?? visual.Weapon.Meshes;
        bool sharedSkeleton = visual.Weapon.NativeWeaponUsesBodySkeleton ?? throw new InvalidDataException("Mixed actor rendering requires the original weapon skeleton binding.");
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
            DrawMesh(weaponObject ? weaponMeshes[0] : bodyMeshes[objectIndex], weaponObject ? visual.Weapon.Textures : visual.Body.Textures, bodyBones, !weaponObject);
        }
        if (!sharedSkeleton && (model.Flags0 & 0x200) == 0)
        {
            NativeBattlePoseSource weaponSource = visual.Weapon.NativePoseSource ?? throw new InvalidDataException("Mixed actor rendering requires the original linked weapon skeleton.");
            ImmutableArray<NativeBattleActionMatrix> weaponBones = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, weaponSource, Identity);
            foreach (BattleMesh mesh in weaponMeshes)
            {
                DrawMesh(mesh, visual.Weapon.Textures, weaponBones, false);
            }
        }

        void DrawMesh(BattleMesh mesh, IReadOnlyList<PixelImage> textures, ImmutableArray<NativeBattleActionMatrix> bones, bool bodyOwner)
        {
            Vector3[] cameraPositions = new Vector3[mesh.Vertices.Count];
            for (int index = 0; index < cameraPositions.Length; index++)
            {
                BattleVertex vertex = mesh.Vertices[index];
                NativeBattleVector position = vertex.NativePosition ?? throw new InvalidDataException("Mixed actor rendering requires the original model vertex WORDs.");
                NativeBattleFullMacVertex posed = NativeBattleGfMatrices.TransformMac460840(bones[vertex.Bone], position);
                cameraPositions[index] = cameraTransform(Vector3.Transform(new(posed.X, posed.Y, posed.Z), visual.OriginToHost));
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
}
