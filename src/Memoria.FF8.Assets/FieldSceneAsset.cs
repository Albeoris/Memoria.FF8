using System.Numerics;

namespace Memoria.FF8.Assets;

public enum FieldSceneAlphaMode
{
    Opaque,
    Mask,
    Blend
}

/// <summary>Sampler values use the glTF 2.0 numeric enums.</summary>
public readonly record struct FieldSceneSampler(int WrapS, int WrapT, int MinFilter, int MagFilter);

public sealed record FieldScenePrimitive(string Id, string NodeId, int MaterialIndex);

public sealed record FieldSceneMaterial(string Id, string Name, Vector4 BaseColorFactor, int Texture, FieldSceneAlphaMode AlphaMode, float AlphaCutoff, bool DoubleSided, bool Unlit, FieldSceneSampler Sampler)
{
    public float MetallicFactor { get; init; } = 1;
    public float RoughnessFactor { get; init; } = 1;
}

/// <summary>Camera looks down its local negative Z axis. Its glTF node may be parented or animated.</summary>
public sealed record FieldSceneCamera(string Id, string Name, int Node, bool Orthographic, float VerticalFieldOfView, float? AspectRatio, float NearPlane, float? FarPlane, float XMagnitude, float YMagnitude)
{
    /// <summary>Optional authored principal point in normalized top-left frame coordinates.</summary>
    public Vector2? PrincipalPoint { get; init; }
}

public sealed record FieldSceneClip(string Id, string Name, int AnimationIndex, float StartTime, float EndTime)
{
    public float Duration => EndTime - StartTime;
}

/// <summary>Independent right-handed, Y-up glTF scene. All matrices use System.Numerics row-vector convention.</summary>
public sealed class FieldSceneAsset
{
    internal FieldSceneAsset(string id, EditableModel geometry, FieldScenePrimitive[] primitives, FieldSceneMaterial[] materials, FieldSceneCamera[] cameras, FieldSceneClip[] clips, string[] warnings)
    {
        Id = id;
        Geometry = geometry;
        Primitives = Array.AsReadOnly(primitives);
        Materials = Array.AsReadOnly(materials);
        Cameras = Array.AsReadOnly(cameras);
        Clips = Array.AsReadOnly(clips);
        Warnings = Array.AsReadOnly(warnings);
    }

    public string Id { get; }
    public EditableModel Geometry { get; }
    public IReadOnlyList<FieldScenePrimitive> Primitives { get; }
    public IReadOnlyList<FieldSceneMaterial> Materials { get; }
    public IReadOnlyList<FieldSceneCamera> Cameras { get; }
    public IReadOnlyList<FieldSceneClip> Clips { get; }
    public IReadOnlyList<string> Warnings { get; }

    public static FieldSceneAsset ImportGlb(ReadOnlySpan<byte> content, string? sceneName = null)
    {
        return ModelExchange.ImportFieldSceneGlb(content, sceneName);
    }

    public Matrix4x4[] SampleTransforms(int animationIndex, float seconds)
    {
        return ModelExchange.SampleFieldTransforms(Geometry, animationIndex, seconds);
    }

    public IReadOnlyList<Vector3[]> PoseNormals(IReadOnlyList<Matrix4x4> transforms)
    {
        Vector3[][] normals = new Vector3[Geometry.Meshes.Count][];
        for (int i = 0; i < normals.Length; i++)
        {
            EditableMesh mesh = Geometry.Meshes[i];
            Vector3[] result = new Vector3[mesh.Vertices.Count];
            if (mesh.Skinned)
            {
                Dictionary<int, Matrix4x4> jointNormals = new(mesh.InverseBindMatrices!.Count);
                foreach ((int joint, Matrix4x4 inverseBind) in mesh.InverseBindMatrices)
                {
                    jointNormals.Add(joint, NormalTransform(inverseBind * transforms[joint]));
                }
                for (int vertexIndex = 0; vertexIndex < result.Length; vertexIndex++)
                {
                    EditableVertex vertex = mesh.Vertices[vertexIndex];
                    Vector3 normal = Vector3.Zero;
                    for (int slot = 0; slot < 4; slot++)
                    {
                        if (vertex.Weights[slot] != 0)
                        {
                            normal += Vector3.TransformNormal(vertex.Normal, jointNormals[(int)vertex.Joints[slot]]) * vertex.Weights[slot];
                        }
                    }
                    result[vertexIndex] = UnitNormal(normal);
                }
            }
            else
            {
                Matrix4x4 normalMatrix = NormalTransform(transforms[mesh.Node]);
                for (int vertexIndex = 0; vertexIndex < result.Length; vertexIndex++)
                {
                    result[vertexIndex] = UnitNormal(Vector3.TransformNormal(mesh.Vertices[vertexIndex].Normal, normalMatrix));
                }
            }
            normals[i] = result;
        }
        return normals;
    }

    private static Matrix4x4 NormalTransform(Matrix4x4 matrix)
    {
        return Matrix4x4.Invert(matrix, out Matrix4x4 inverse) ? Matrix4x4.Transpose(inverse) : default;
    }

    private static Vector3 UnitNormal(Vector3 normal)
    {
        float length = normal.LengthSquared();
        return float.IsFinite(length) && length > 0 ? normal / MathF.Sqrt(length) : Vector3.Zero;
    }
}
