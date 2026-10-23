using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

/// <summary>Editable glTF-space model. Positions use a right-handed, Y-up coordinate system.</summary>
public sealed record EditableModel(string Name, string Kind, IReadOnlyList<EditableBone> Bones, IReadOnlyList<EditableMesh> Meshes, IReadOnlyList<PixelImage> Textures, IReadOnlyList<EditableAnimation> Animations, JsonElement NativeMetadata)
{
    public float NativeFramesPerSecond { get; init; } = FramesPerSecondDefault;
    private const float FramesPerSecondDefault = 30;
    public string? ExchangeManifest { get; init; }
}
public sealed record EditableBone(string Name, int Parent, Vector3 Translation, Quaternion Rotation, Vector3 Scale, bool IsJoint = true)
{
    public int NativeIndex { get; init; } = -1;
    public string? RigId { get; init; }
    public string? StableId { get; init; }
}
public readonly record struct EditableVertex(Vector3 Position, Vector3 Normal, Vector2 Uv, Vector4 Joints, Vector4 Weights, Vector4 Color);
public sealed record EditableMesh(string Name, IReadOnlyList<EditableVertex> Vertices, IReadOnlyList<int> Indices, int Texture, Matrix4x4 Transform)
{
    public int Node { get; init; } = -1;
    public int NativePart { get; init; } = -1;
    public string? ObjectTarget { get; init; }
    public string? RigId { get; init; }
    public bool Skinned { get; init; }
    public IReadOnlyDictionary<int, Matrix4x4>? InverseBindMatrices { get; init; }
    public bool Transparent { get; init; }
    public bool DoubleSided { get; init; } = true;
}
public sealed record EditableAnimation(string Name, IReadOnlyList<EditableAnimationChannel> Channels)
{
    public float Duration => Channels.SelectMany(channel => channel.Times).DefaultIfEmpty(0).Max();
}
public sealed record EditableAnimationChannel(int Bone, string Path, float[] Times, Vector4[] Values, string Interpolation = "LINEAR");
public sealed record ModelExchangeImportResult(EditableModel Model, IReadOnlyList<string> Warnings, bool RequiresBoneCountApproval);

public static partial class ModelExchange
{
    public const float FramesPerSecond = 30;
    public const float WorldFramesPerSecond = 30.5f;
    private static readonly Matrix4x4 NativeToGltf = Matrix4x4.CreateRotationX(-MathF.PI / 2);
    private static readonly Matrix4x4 GltfToNative = Matrix4x4.CreateRotationX(MathF.PI / 2);
    private static readonly JsonSerializerOptions MetadataOptions = new() { IncludeFields = true, Converters = { new NativeArrayConverterFactory() } };

    public static EditableModel FromField(FieldModel model, float framesPerSecond = FramesPerSecond)
    {
        if (model.EditableOverride is { } edited)
        {
            return edited;
        }
        EditableBone[] bones = model.Bones.Select((bone, index) => Bone(index, bone.Parent, bone.Parent < 0 ? Matrix4x4.CreateRotationY(MathF.PI / 2) : Matrix4x4.CreateTranslation(0, 0, model.Bones[bone.Parent].Length))).ToArray();
        Matrix4x4[] bind = Globals(bones);
        List<EditableMesh> meshes = [];
        foreach (IGrouping<(int Texture, bool Transparent), ModelFace> group in model.Faces.GroupBy(face => (face.Texture, face.SemiTransparent)))
        {
            List<EditableVertex> vertices = [];
            List<int> indices = [];
            foreach (ModelFace face in group)
            {
                AddFace(vertices, indices, face.Vertices.Take(face.Quad ? 4 : 3).Select((index, corner) => Vertex(model.Vertices[index].Position, model.Vertices[index].Bone, face.TextureCoordinates[corner] / ResourceTextureList.LogicalSize(model.Textures, face.Texture), face.VertexColors?[corner] ?? Vector4.One, bind)).ToArray());
            }
            meshes.Add(new EditableMesh($"mesh_{meshes.Count}", vertices, indices, group.Key.Texture, Matrix4x4.Identity) { Skinned = bones.Length != 0, Transparent = group.Key.Transparent });
        }
        EditableAnimation[] animations = model.Animations.Select((animation, index) => Animation($"animation_{index}", bones, animation.Frames.Count, (bone, frame) =>
        {
            Vector3 angles = animation.Frames[frame].Rotations[bone];
            Matrix4x4 local = Matrix4x4.CreateRotationZ(angles.Z) * Matrix4x4.CreateRotationY(angles.Y) * Matrix4x4.CreateRotationX(angles.X);
            int parent = model.Bones[bone].Parent;
            if (parent < 0)
            {
                local *= Matrix4x4.CreateRotationY(MathF.PI / 2);
                local.Translation = animation.Frames[frame].Translation;
            }
            else
            {
                local.Translation = new Vector3(0, 0, model.Bones[parent].Length);
            }
            return ConvertTransform(local);
        }, framesPerSecond)).ToArray();
        return new EditableModel(model.Name, "field", bones, meshes, model.Textures, animations, Metadata(model)) { NativeFramesPerSecond = framesPerSecond };
    }

    public static EditableModel FromBattle(BattleModel model)
    {
        if (model.EditableOverride is { } edited)
        {
            return edited;
        }
        EditableBone[] bones = model.Bones.Select((bone, index) => Bone(index, bone.Parent, Matrix4x4.CreateTranslation(0, 0, bone.Parent < 0 ? 0 : model.Bones[bone.Parent].Length * model.Scale))).ToArray();
        Matrix4x4[] bind = Globals(bones);
        List<EditableMesh> meshes = BattleMeshes(model.Meshes, model.Textures, bind, model.Scale, bones.Length != 0);
        EditableAnimation[] animations = model.Animations.Select((animation, index) => Animation($"animation_{index}", bones, animation.Frames.Count, (bone, frame) =>
        {
            BattlePose pose = animation.Frames[frame];
            Vector3 angles = pose.Rotations[bone];
            Matrix4x4 local = Matrix4x4.CreateScale(pose.Scales[bone]) * Matrix4x4.CreateRotationZ(angles.Z) * Matrix4x4.CreateRotationX(-angles.X) * Matrix4x4.CreateRotationY(-angles.Y);
            int parent = model.Bones[bone].Parent;
            local.Translation = parent < 0 ? new Vector3(-pose.Translation.X, -pose.Translation.Y, pose.Translation.Z) * model.Scale : new Vector3(0, 0, model.Bones[parent].Length * model.Scale);
            return ConvertTransform(local);
        })).ToArray();
        return new EditableModel(model.Name, "battle", bones, meshes, model.Textures, animations, Metadata(model));
    }

    public static EditableModel FromStage(BattleStage model)
    {
        return new EditableModel($"stage_{model.Id}", "stage", [], BattleMeshes(model.Meshes, model.Textures, [], 1, false), model.Textures, [], Metadata(model));
    }

    public static EditableModel FromWorld(WorldStaticModel model)
    {
        List<EditableMesh> meshes = [];
        foreach (IGrouping<(int Palette, bool Transparent), WorldStaticFace> group in model.Faces.GroupBy(face => (face.Palette, face.Transparent)))
        {
            List<EditableVertex> vertices = [];
            List<int> indices = [];
            foreach (WorldStaticFace face in group)
            {
                AddFace(vertices, indices, face.Vertices.Select((index, corner) => Vertex(model.NativeVertices[index], -1, face.TextureCoordinates[corner] / ResourceTextureList.LogicalSize(model.Textures, face.Palette), face.VertexColors?[corner] ?? Vector4.One, [])).ToArray());
            }
            meshes.Add(new EditableMesh($"mesh_{meshes.Count}", vertices, indices, group.Key.Palette, Matrix4x4.Identity) { Transparent = group.Key.Transparent });
        }
        return new EditableModel($"world_{model.ShapeId}_{model.ModelIndex}", "world", [], meshes, model.Textures, [], Metadata(model));
    }

    public static FieldModel ToField(EditableModel edited, FieldModel original)
    {
        edited = WithFallbackTexture(edited);
        List<ModelVertex> vertices = [];
        List<ModelFace> faces = [];
        foreach (EditableMesh mesh in edited.Meshes)
        {
            int offset = vertices.Count;
            vertices.AddRange(mesh.Vertices.Select(vertex => new ModelVertex(Vector3.Transform(vertex.Position, GltfToNative), 0)));
            for (int index = 0; index < mesh.Indices.Count; index += 3)
            {
                int[] triangle = [mesh.Indices[index], mesh.Indices[index + 1], mesh.Indices[index + 2]];
                faces.Add(new ModelFace(triangle.Select(value => value + offset).ToArray(), triangle.Select(value => mesh.Vertices[value].Uv * ResourceTextureList.LogicalSize(edited.Textures, mesh.Texture)).ToArray(), mesh.Texture, false) { SemiTransparent = mesh.Transparent, VertexColors = triangle.Select(value => mesh.Vertices[value].Color).ToArray() });
            }
        }
        return original with { Vertices = vertices, Faces = faces, Textures = edited.Textures, EditableOverride = edited };
    }

    public static BattleModel ToBattle(EditableModel edited, BattleModel original)
    {
        edited = WithFallbackTexture(edited);
        return original with { Meshes = NativeBattleMeshes(edited), Textures = edited.Textures, EditableOverride = edited };
    }

    public static BattleStage ToStage(EditableModel edited, BattleStage original)
    {
        edited = WithFallbackTexture(edited);
        return original with { Meshes = NativeBattleMeshes(edited), Textures = edited.Textures };
    }

    public static WorldStaticModel ToWorld(EditableModel edited, WorldStaticModel original)
    {
        edited = WithFallbackTexture(edited);
        IReadOnlyList<Vector3[]> pose = Pose(edited, -1, 0);
        List<Vector3> vertices = [];
        List<WorldStaticFace> faces = [];
        for (int meshIndex = 0; meshIndex < edited.Meshes.Count; meshIndex++)
        {
            EditableMesh mesh = edited.Meshes[meshIndex];
            int offset = vertices.Count;
            vertices.AddRange(pose[meshIndex].Select(value => Vector3.Transform(value, GltfToNative)));
            for (int index = 0; index < mesh.Indices.Count; index += 3)
            {
                int[] triangle = [mesh.Indices[index], mesh.Indices[index + 1], mesh.Indices[index + 2]];
                faces.Add(new WorldStaticFace(triangle.Select(value => value + offset).ToArray(), triangle.Select(value => mesh.Vertices[value].Uv * ResourceTextureList.LogicalSize(edited.Textures, mesh.Texture)).ToArray(), mesh.Texture, mesh.Transparent) { VertexColors = triangle.Select(value => mesh.Vertices[value].Color).ToArray() });
            }
        }
        return original with { NativeVertices = vertices, Faces = faces, Textures = edited.Textures };
    }

    public static IReadOnlyList<Vector3[]> Pose(EditableModel model, int animation, float seconds)
    {
        EditableBone[] bones = model.Bones.ToArray();
        if (animation >= 0 && animation < model.Animations.Count)
        {
            foreach (EditableAnimationChannel channel in model.Animations[animation].Channels)
            {
                Vector4 value = Sample(channel, seconds);
                EditableBone bone = bones[channel.Bone];
                bones[channel.Bone] = channel.Path switch
                {
                    "translation" => bone with { Translation = new Vector3(value.X, value.Y, value.Z) },
                    "rotation" => bone with { Rotation = Quaternion.Normalize(new Quaternion(value.X, value.Y, value.Z, value.W)) },
                    "scale" => bone with { Scale = new Vector3(value.X, value.Y, value.Z) },
                    _ => throw new InvalidDataException($"Unsupported animation channel {channel.Path}.")
                };
            }
        }
        return PoseWithTransforms(model, Globals(bones));
    }

    public static IReadOnlyList<Vector3[]> PoseWithTransforms(EditableModel model, IReadOnlyList<Matrix4x4> transforms)
    {
        Matrix4x4[] bind = Globals(model.Bones);
        Matrix4x4[] inverse = bind.Select(matrix => Matrix4x4.Invert(matrix, out Matrix4x4 result) ? result : throw new InvalidDataException("A bind transform is singular.")).ToArray();
        return model.Meshes.Select(mesh => mesh.Vertices.Select(vertex =>
        {
            if (!mesh.Skinned)
            {
                return Vector3.Transform(vertex.Position, mesh.Node >= 0 ? transforms[mesh.Node] : mesh.Transform);
            }
            Vector3 result = Vector3.Zero;
            for (int slot = 0; slot < 4; slot++)
            {
                float weight = vertex.Weights[slot];
                if (weight == 0)
                {
                    continue;
                }
                int joint = (int)vertex.Joints[slot];
                Matrix4x4 inverseBind = mesh.InverseBindMatrices is not null && mesh.InverseBindMatrices.TryGetValue(joint, out Matrix4x4 supplied) ? supplied : inverse[joint];
                result += Vector3.Transform(vertex.Position, inverseBind * transforms[joint]) * weight;
            }
            return result;
        }).ToArray()).ToArray();
    }

    internal static Vector3 ToNative(Vector3 value) => Vector3.Transform(value, GltfToNative);
    internal static Matrix4x4 ConvertTransform(Matrix4x4 value) => GltfToNative * value * NativeToGltf;

    internal static Vector4 Sample(EditableAnimationChannel channel, float seconds)
    {
        int index = Array.BinarySearch(channel.Times, seconds);
        if (index < 0)
        {
            index = Math.Max(0, ~index - 1);
        }
        int next = Math.Min(index + 1, channel.Times.Length - 1);
        float fraction = next == index ? 0 : Math.Clamp((seconds - channel.Times[index]) / (channel.Times[next] - channel.Times[index]), 0, 1);
        if (channel.Interpolation == "STEP")
        {
            return channel.Values[index];
        }
        if (channel.Interpolation == "CUBICSPLINE")
        {
            float duration = next == index ? 0 : channel.Times[next] - channel.Times[index];
            float square = fraction * fraction;
            float cube = square * fraction;
            return (2 * cube - 3 * square + 1) * channel.Values[index * 3 + 1] + (cube - 2 * square + fraction) * duration * channel.Values[index * 3 + 2] + (-2 * cube + 3 * square) * channel.Values[next * 3 + 1] + (cube - square) * duration * channel.Values[next * 3];
        }
        if (channel.Path == "rotation")
        {
            Vector4 a = channel.Values[index];
            Vector4 b = channel.Values[next];
            Quaternion result = Quaternion.Slerp(new Quaternion(a.X, a.Y, a.Z, a.W), new Quaternion(b.X, b.Y, b.Z, b.W), fraction);
            return new Vector4(result.X, result.Y, result.Z, result.W);
        }
        return Vector4.Lerp(channel.Values[index], channel.Values[next], fraction);
    }

    internal static Matrix4x4[] Globals(IReadOnlyList<EditableBone> bones)
    {
        Matrix4x4[] result = new Matrix4x4[bones.Count];
        byte[] visited = new byte[bones.Count];
        Matrix4x4 Resolve(int index)
        {
            if (visited[index] == 1)
            {
                throw new InvalidDataException("The node hierarchy contains a cycle.");
            }
            if (visited[index] == 2)
            {
                return result[index];
            }
            visited[index] = 1;
            EditableBone bone = bones[index];
            if (bone.Parent >= bones.Count || bone.Parent < -1)
            {
                throw new InvalidDataException("A node parent is out of range.");
            }
            Matrix4x4 local = Matrix4x4.CreateScale(bone.Scale) * Matrix4x4.CreateFromQuaternion(bone.Rotation) * Matrix4x4.CreateTranslation(bone.Translation);
            result[index] = bone.Parent < 0 ? local : local * Resolve(bone.Parent);
            visited[index] = 2;
            return result[index];
        }
        for (int index = 0; index < bones.Count; index++)
        {
            Resolve(index);
        }
        return result;
    }

    private static EditableBone Bone(int index, int parent, Matrix4x4 native)
    {
        Matrix4x4 converted = ConvertTransform(native);
        Matrix4x4.Decompose(converted, out Vector3 scale, out Quaternion rotation, out Vector3 translation);
        return new EditableBone($"bone_{index}", parent, translation, rotation, scale) { NativeIndex = index };
    }

    public static int[] NativeJointOrder(EditableModel model)
    {
        int NativeIndex(int index)
        {
            EditableBone bone = model.Bones[index];
            if (bone.NativeIndex >= 0)
            {
                return bone.NativeIndex;
            }
            return bone.Name.StartsWith("bone_", StringComparison.Ordinal) && int.TryParse(bone.Name.AsSpan(5), out int native) ? native : index;
        }
        return Enumerable.Range(0, model.Bones.Count).Where(index => model.Bones[index].IsJoint).OrderBy(NativeIndex).ToArray();
    }

    private static JsonElement Metadata<T>(T model) => JsonSerializer.SerializeToElement(model, MetadataOptions);

    private sealed class NativeArrayConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>);

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) => (JsonConverter)Activator.CreateInstance(typeof(NativeArrayConverter<>).MakeGenericType(type.GetGenericArguments()[0]))!;
    }

    private sealed class NativeArrayConverter<T> : JsonConverter<System.Collections.Immutable.ImmutableArray<T>>
    {
        public override System.Collections.Immutable.ImmutableArray<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException("Native metadata is retained as an opaque payload.");

        public override void Write(Utf8JsonWriter writer, System.Collections.Immutable.ImmutableArray<T> value, JsonSerializerOptions options)
        {
            if (value.IsDefault)
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteStartArray();
            foreach (T item in value)
            {
                JsonSerializer.Serialize(writer, item, options);
            }
            writer.WriteEndArray();
        }
    }

    private static EditableVertex Vertex(Vector3 position, int bone, Vector2 uv, Vector4 color, IReadOnlyList<Matrix4x4> bind)
    {
        Vector3 converted = Vector3.Transform(position, NativeToGltf);
        return new EditableVertex(bone < 0 ? converted : Vector3.Transform(converted, bind[bone]), Vector3.Zero, uv, new Vector4(Math.Max(0, bone), 0, 0, 0), bone < 0 ? Vector4.Zero : new Vector4(1, 0, 0, 0), color);
    }

    private static void AddFace(List<EditableVertex> vertices, List<int> indices, EditableVertex[] corners, bool battle = false)
    {
        if (corners.Length is not (3 or 4))
        {
            throw new InvalidDataException("Only triangular and quadrilateral native faces are supported.");
        }
        int offset = vertices.Count;
        Vector3 normal = Vector3.Cross(corners[1].Position - corners[0].Position, corners[2].Position - corners[0].Position);
        normal = normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : Vector3.UnitY;
        vertices.AddRange(corners.Select(vertex => vertex with { Normal = normal }));
        indices.AddRange([offset, offset + 1, offset + (battle && corners.Length == 4 ? 3 : 2)]);
        if (corners.Length == 4)
        {
            indices.AddRange(battle ? [offset + 3, offset + 2, offset] : [offset + 2, offset + 1, offset + 3]);
        }
    }

    private static List<EditableMesh> BattleMeshes(IReadOnlyList<BattleMesh> source, IReadOnlyList<PixelImage> textures, Matrix4x4[] bind, float scale, bool skinned)
    {
        List<EditableMesh> result = [];
        for (int part = 0; part < source.Count; part++)
        {
            BattleMesh mesh = source[part];
            foreach (IGrouping<int, BattleFace> group in mesh.Faces.GroupBy(face => face.Texture))
            {
                List<EditableVertex> vertices = [];
                List<int> indices = [];
                foreach (BattleFace face in group)
                {
                    Vector4 color = new(Math.Min(1, face.Color.R / 128f), Math.Min(1, face.Color.G / 128f), Math.Min(1, face.Color.B / 128f), face.Color.A / 255f);
                    AddFace(vertices, indices, face.Vertices.Select((index, corner) => Vertex(mesh.Vertices[index].Position * scale, skinned ? mesh.Vertices[index].Bone : -1, face.Uv[corner] / ResourceTextureList.LogicalSize(textures, face.Texture), color * (face.VertexColors?[corner] ?? Vector4.One), bind)).ToArray(), true);
                }
                result.Add(new EditableMesh($"mesh_{result.Count}", vertices, indices, group.Key, Matrix4x4.Identity) { Skinned = skinned, NativePart = part });
            }
        }
        return result;
    }

    internal static IReadOnlyList<int[]> BattleMeshPartGroups(EditableModel model)
    {
        if (model.Meshes.Any(mesh => mesh.NativePart > 4095))
        {
            throw new InvalidDataException("Native geometry part index exceeds supported limits.");
        }
        int nextPart = model.Meshes.Where(mesh => mesh.NativePart >= 0).Select(mesh => mesh.NativePart + 1).DefaultIfEmpty(0).Max();
        Dictionary<int, List<int>> groups = [];
        for (int index = 0; index < model.Meshes.Count; index++)
        {
            int part = model.Meshes[index].NativePart >= 0 ? model.Meshes[index].NativePart : nextPart++;
            if (!groups.TryGetValue(part, out List<int>? meshes))
            {
                meshes = [];
                groups.Add(part, meshes);
            }
            meshes.Add(index);
        }
        int count = groups.Keys.Select(part => part + 1).DefaultIfEmpty(0).Max();
        return Enumerable.Range(0, count).Select(part => groups.TryGetValue(part, out List<int>? meshes) ? meshes.ToArray() : []).ToArray();
    }

    internal static IReadOnlyList<Vector3[]> NativeBattlePartPose(EditableModel model, IReadOnlyList<Vector3[]> pose)
    {
        if (pose.Count != model.Meshes.Count)
        {
            throw new InvalidDataException("Editable pose primitives do not match their geometry.");
        }
        return BattleMeshPartGroups(model).Select(group => group.SelectMany(index => pose[index]).Select(ToNative).ToArray()).ToArray();
    }

    private static IReadOnlyList<BattleMesh> NativeBattleMeshes(EditableModel model)
    {
        IReadOnlyList<Vector3[]> pose = Pose(model, -1, 0);
        List<BattleMesh> parts = [];
        foreach (int[] group in BattleMeshPartGroups(model))
        {
            List<BattleVertex> vertices = [];
            List<BattleFace> faces = [];
            foreach (int meshIndex in group)
            {
                EditableMesh mesh = model.Meshes[meshIndex];
                int offset = vertices.Count;
                vertices.AddRange(pose[meshIndex].Select(position => new BattleVertex(ToNative(position), 0)));
                for (int index = 0; index < mesh.Indices.Count; index += 3)
                {
                    int[] triangle = [mesh.Indices[index], mesh.Indices[index + 1], mesh.Indices[index + 2]];
                    faces.Add(new BattleFace(triangle.Select(value => value + offset).ToArray(), triangle.Select(value => mesh.Vertices[value].Uv * ResourceTextureList.LogicalSize(model.Textures, mesh.Texture)).ToArray(), mesh.Texture, new Rgba32(128, 128, 128)) { VertexColors = triangle.Select(value => mesh.Vertices[value].Color).ToArray() });
                }
            }
            parts.Add(new BattleMesh(vertices, faces));
        }
        return parts;
    }

    private static EditableModel WithFallbackTexture(EditableModel model)
    {
        if (model.Meshes.All(mesh => mesh.Texture >= 0))
        {
            return model;
        }
        int fallback = model.Textures.Count;
        return model with
        {
            Textures = model.Textures.Concat([new PixelImage(1, 1, [new Rgba32(255, 255, 255)])]).ToArray(),
            Meshes = model.Meshes.Select(mesh => mesh.Texture < 0 ? mesh with { Texture = fallback } : mesh).ToArray()
        };
    }

    private static EditableAnimation Animation(string name, IReadOnlyList<EditableBone> bones, int count, Func<int, int, Matrix4x4> read, float framesPerSecond = FramesPerSecond)
    {
        List<EditableAnimationChannel> channels = [];
        if (count == 0)
        {
            return new EditableAnimation(name, channels);
        }
        float[] times = Enumerable.Range(0, count).Select(frame => frame / framesPerSecond).ToArray();
        for (int bone = 0; bone < bones.Count; bone++)
        {
            Vector4[] translations = new Vector4[count];
            Vector4[] rotations = new Vector4[count];
            Vector4[] scales = new Vector4[count];
            for (int frame = 0; frame < count; frame++)
            {
                Matrix4x4.Decompose(read(bone, frame), out Vector3 scale, out Quaternion rotation, out Vector3 translation);
                rotation = Quaternion.Normalize(rotation);
                if (frame > 0 && Vector4.Dot(rotations[frame - 1], new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W)) < 0)
                {
                    rotation = new Quaternion(-rotation.X, -rotation.Y, -rotation.Z, -rotation.W);
                }
                translations[frame] = new Vector4(translation, 0);
                rotations[frame] = new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W);
                scales[frame] = new Vector4(scale, 0);
            }
            channels.Add(new EditableAnimationChannel(bone, "translation", times, translations));
            channels.Add(new EditableAnimationChannel(bone, "rotation", times, rotations));
            channels.Add(new EditableAnimationChannel(bone, "scale", times, scales));
        }
        return new EditableAnimation(name, channels);
    }
}
