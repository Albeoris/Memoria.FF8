using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;

namespace Memoria.FF8.Assets;

public static partial class ModelExchange
{
    public static ModelExchangeImportResult ImportGlb(ReadOnlySpan<byte> content, EditableModel? original = null, bool allowBoneCountChange = false)
    {
        if (content.Length < 20 || content.Length > 256 * 1024 * 1024 || BinaryPrimitives.ReadUInt32LittleEndian(content) != 0x46546c67 || BinaryPrimitives.ReadUInt32LittleEndian(content[4..]) != 2 || BinaryPrimitives.ReadUInt32LittleEndian(content[8..]) != content.Length)
        {
            throw new InvalidDataException("Expected a complete glTF 2.0 binary file no larger than 256 MiB.");
        }
        byte[]? json = null;
        byte[] binary = [];
        int offset = 12;
        while (offset < content.Length)
        {
            if (content.Length - offset < 8)
            {
                throw new InvalidDataException("Truncated GLB chunk header.");
            }
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(content[offset..]);
            uint kind = BinaryPrimitives.ReadUInt32LittleEndian(content[(offset + 4)..]);
            offset += 8;
            if (length % 4 != 0 || length > content.Length - offset)
            {
                throw new InvalidDataException("Invalid GLB chunk length.");
            }
            if (kind == 0x4e4f534a)
            {
                if (json is not null || offset != 20)
                {
                    throw new InvalidDataException("The JSON chunk must occur exactly once and first.");
                }
                json = content.Slice(offset, (int)length).ToArray();
            }
            else if (kind == 0x004e4942)
            {
                if (binary.Length != 0 || json is null)
                {
                    throw new InvalidDataException("Invalid binary chunk order.");
                }
                binary = content.Slice(offset, (int)length).ToArray();
            }
            offset += (int)length;
        }
        if (json is null)
        {
            throw new InvalidDataException("The GLB JSON chunk is missing.");
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 128 });
            return new GlbReader(document.RootElement, binary).Read(original, allowBoneCountChange);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException or OverflowException or InvalidOperationException or MagickException)
        {
            throw new InvalidDataException("The GLB contains invalid or unsupported structural data.", exception);
        }
    }

    public static void Validate(EditableModel model)
    {
        if (model.Bones.Count > 4096 || model.Meshes.Count > 4096 || model.Textures.Count > 1024)
        {
            throw new InvalidDataException("The model exceeds supported resource limits.");
        }
        foreach (EditableBone bone in model.Bones)
        {
            Finite(new Vector4(bone.Translation, 0));
            Finite(new Vector4(bone.Scale, 0));
            Finite(new Vector4(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W));
            if (MathF.Abs(bone.Rotation.LengthSquared() - 1) > 0.001f)
            {
                throw new InvalidDataException("Node rotation must be a unit quaternion.");
            }
            int parent = bone.Parent;
            int depth = 0;
            while (parent >= 0)
            {
                if (parent >= model.Bones.Count || ++depth > 128)
                {
                    throw new InvalidDataException("Invalid, cyclic or excessively deep node hierarchy.");
                }
                parent = model.Bones[parent].Parent;
            }
        }
        Globals(model.Bones);
        long vertexCount = 0;
        foreach (EditableMesh mesh in model.Meshes)
        {
            vertexCount += mesh.Vertices.Count;
            if (vertexCount > 4_000_000 || mesh.Vertices.Count == 0 || mesh.Indices.Count == 0 || mesh.Indices.Count % 3 != 0 || mesh.Texture < -1 || mesh.Texture >= model.Textures.Count || mesh.Node < -1 || mesh.Node >= model.Bones.Count)
            {
                throw new InvalidDataException("Invalid mesh geometry, node or material reference.");
            }
            foreach (int index in mesh.Indices)
            {
                if ((uint)index >= mesh.Vertices.Count)
                {
                    throw new InvalidDataException("A triangle index is out of range.");
                }
            }
            foreach (float value in MatrixValues(mesh.Transform))
            {
                if (!float.IsFinite(value))
                {
                    throw new InvalidDataException("Mesh transforms must be finite.");
                }
            }
            foreach (EditableVertex vertex in mesh.Vertices)
            {
                Finite(new Vector4(vertex.Position, 0));
                Finite(new Vector4(vertex.Normal, 0));
                Finite(new Vector4(vertex.Uv, 0, 0));
                Finite(vertex.Color);
                if (vertex.Color.X < 0 || vertex.Color.Y < 0 || vertex.Color.Z < 0 || vertex.Color.W < 0 || vertex.Color.X > 1 || vertex.Color.Y > 1 || vertex.Color.Z > 1 || vertex.Color.W > 1)
                {
                    throw new InvalidDataException("Vertex colors must lie between zero and one.");
                }
                Finite(vertex.Joints);
                Finite(vertex.Weights);
                if (!mesh.Skinned)
                {
                    continue;
                }
                float sum = 0;
                for (int slot = 0; slot < 4; slot++)
                {
                    float weight = vertex.Weights[slot];
                    float joint = vertex.Joints[slot];
                    if (weight < 0 || weight > 1 || weight != 0 && (joint < 0 || joint >= model.Bones.Count || joint != MathF.Truncate(joint) || !model.Bones[(int)joint].IsJoint))
                    {
                        throw new InvalidDataException("Invalid skin weight or joint reference.");
                    }
                    sum += weight;
                }
                if (MathF.Abs(sum - 1) > 0.001f)
                {
                    throw new InvalidDataException("Skin weights must sum to one.");
                }
            }
            if (mesh.InverseBindMatrices is not null)
            {
                foreach ((int bone, Matrix4x4 matrix) in mesh.InverseBindMatrices)
                {
                    if ((uint)bone >= model.Bones.Count || !Matrix4x4.Invert(matrix, out _) || MatrixValues(matrix).Any(value => !float.IsFinite(value)))
                    {
                        throw new InvalidDataException("An inverse bind matrix is invalid or singular.");
                    }
                }
            }
        }
        foreach (EditableAnimation animation in model.Animations)
        {
            HashSet<(int, string)> targets = [];
            foreach (EditableAnimationChannel channel in animation.Channels)
            {
                if ((uint)channel.Bone >= model.Bones.Count || channel.Path is not ("translation" or "rotation" or "scale") || channel.Interpolation is not ("LINEAR" or "STEP" or "CUBICSPLINE") || channel.Times.Length == 0 || channel.Values.Length != channel.Times.Length * (channel.Interpolation == "CUBICSPLINE" ? 3 : 1) || !targets.Add((channel.Bone, channel.Path)))
                {
                    throw new InvalidDataException("Invalid or duplicate animation channel.");
                }
                for (int index = 0; index < channel.Times.Length; index++)
                {
                    float time = channel.Times[index];
                    if (!float.IsFinite(time) || time < 0 || index != 0 && time <= channel.Times[index - 1])
                    {
                        throw new InvalidDataException("Animation key times must be finite, nonnegative and strictly increasing.");
                    }
                }
                foreach (Vector4 value in channel.Values)
                {
                    Finite(value);
                }
                if (channel.Path == "rotation")
                {
                    for (int index = channel.Interpolation == "CUBICSPLINE" ? 1 : 0; index < channel.Values.Length; index += channel.Interpolation == "CUBICSPLINE" ? 3 : 1)
                    {
                        if (MathF.Abs(channel.Values[index].LengthSquared() - 1) > 0.001f)
                        {
                            throw new InvalidDataException("Animation rotations must be unit quaternions.");
                        }
                    }
                }
            }
        }
    }

    private static void Finite(Vector4 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) || !float.IsFinite(value.W))
        {
            throw new InvalidDataException("Model values must be finite.");
        }
    }

    private sealed class GlbReader(JsonElement root, byte[] binary)
    {
        private readonly List<string> _warnings = [];
        private readonly Dictionary<int, float[][]> _accessorCache = [];

        public ModelExchangeImportResult Read(EditableModel? original, bool allowBoneCountChange)
        {
            if (root.GetProperty("asset").GetProperty("version").GetString() != "2.0")
            {
                throw new InvalidDataException("Only glTF 2.0 is supported.");
            }
            if (root.TryGetProperty("extensionsRequired", out JsonElement required) && required.GetArrayLength() != 0)
            {
                throw new InvalidDataException("Required glTF extensions are unsupported; export standard, uncompressed glTF 2.0.");
            }
            JsonElement buffers = root.GetProperty("buffers");
            if (buffers.GetArrayLength() != 1 || buffers[0].TryGetProperty("uri", out _) || Int(buffers[0], "byteLength") > binary.Length || binary.Length - Int(buffers[0], "byteLength") > 3)
            {
                throw new InvalidDataException("Expected exactly one self-contained GLB buffer.");
            }
            JsonElement nodes = root.GetProperty("nodes");
            if (nodes.GetArrayLength() > 4096)
            {
                throw new InvalidDataException("Too many nodes.");
            }
            int[] parents = Enumerable.Repeat(-1, nodes.GetArrayLength()).ToArray();
            for (int index = 0; index < nodes.GetArrayLength(); index++)
            {
                if (!nodes[index].TryGetProperty("children", out JsonElement children))
                {
                    continue;
                }
                foreach (JsonElement child in children.EnumerateArray())
                {
                    int target = child.GetInt32();
                    if ((uint)target >= parents.Length || parents[target] >= 0 || target == index)
                    {
                        throw new InvalidDataException("Invalid or multiply parented node hierarchy.");
                    }
                    parents[target] = index;
                }
            }
            HashSet<int> joints = [];
            if (root.TryGetProperty("skins", out JsonElement skinArray))
            {
                foreach (JsonElement skin in skinArray.EnumerateArray())
                {
                    HashSet<int> unique = [];
                    foreach (JsonElement joint in skin.GetProperty("joints").EnumerateArray())
                    {
                        int index = joint.GetInt32();
                        if ((uint)index >= nodes.GetArrayLength() || !unique.Add(index))
                        {
                            throw new InvalidDataException("Invalid or duplicate skin joint.");
                        }
                        joints.Add(index);
                    }
                }
            }
            List<EditableBone> bones = [];
            string name = original?.Name ?? "Imported model";
            string kind = original?.Kind ?? "model";
            JsonElement metadata = original?.NativeMetadata ?? default;
            for (int index = 0; index < nodes.GetArrayLength(); index++)
            {
                JsonElement node = nodes[index];
                Vector3 translation = Vec3(node, "translation", Vector3.Zero);
                Vector3 scale = Vec3(node, "scale", Vector3.One);
                Vector4 rotationValue = Vec4(node, "rotation", new Vector4(0, 0, 0, 1));
                Quaternion rotation = new(rotationValue.X, rotationValue.Y, rotationValue.Z, rotationValue.W);
                if (node.TryGetProperty("matrix", out JsonElement matrix))
                {
                    Matrix4x4 transform = Matrix(matrix.EnumerateArray().Select(value => value.GetSingle()).ToArray());
                    if (node.TryGetProperty("translation", out _) || node.TryGetProperty("rotation", out _) || node.TryGetProperty("scale", out _) || !Matrix4x4.Decompose(transform, out scale, out rotation, out translation))
                    {
                        throw new InvalidDataException("Node matrix cannot be represented as translation, rotation and scale.");
                    }
                    Matrix4x4 reconstructed = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
                    if (MatrixValues(transform).Zip(MatrixValues(reconstructed)).Any(pair => !float.IsFinite(pair.First) || MathF.Abs(pair.First - pair.Second) > 0.0001f * MathF.Max(1, MathF.Abs(pair.First))))
                    {
                        throw new InvalidDataException("Apply shear and perspective transforms before exporting the model.");
                    }
                }
                int nativeIndex = node.TryGetProperty("extras", out JsonElement boneExtras) && boneExtras.TryGetProperty("FF8_bone_index", out JsonElement nativeId) ? nativeId.GetInt32() : -1;
                bones.Add(new EditableBone(String(node, "name", $"node_{index}"), parents[index], translation, rotation, scale, joints.Contains(index)) { NativeIndex = nativeIndex });
                if (node.TryGetProperty("extras", out JsonElement extras) && extras.TryGetProperty("FF8_native", out JsonElement payload) && payload.ValueKind == JsonValueKind.String)
                {
                    using JsonDocument native = JsonDocument.Parse(payload.GetString()!, new JsonDocumentOptions { MaxDepth = 128 });
                    metadata = native.RootElement.Clone();
                    kind = String(extras, "FF8_kind", kind);
                    name = String(node, "name", name);
                }
            }
            // Validate hierarchy before computing world transforms.
            Validate(new EditableModel(name, kind, bones, [], [], [], metadata));
            Matrix4x4[] globals = Globals(bones);
            List<PixelImage> textures = ReadTextures();
            List<EditableMesh> meshes = [];
            int totalVertices = 0;
            for (int nodeIndex = 0; nodeIndex < nodes.GetArrayLength(); nodeIndex++)
            {
                JsonElement node = nodes[nodeIndex];
                if (!node.TryGetProperty("mesh", out JsonElement meshReference))
                {
                    continue;
                }
                JsonElement mesh = At("meshes", meshReference.GetInt32());
                int[] skinJoints = [];
                Dictionary<int, Matrix4x4>? inverseBind = null;
                if (node.TryGetProperty("skin", out JsonElement skinReference))
                {
                    JsonElement skin = At("skins", skinReference.GetInt32());
                    skinJoints = skin.GetProperty("joints").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    inverseBind = [];
                    if (skin.TryGetProperty("inverseBindMatrices", out JsonElement inverseReference))
                    {
                        float[][] values = Accessor(inverseReference.GetInt32(), "MAT4", 5126);
                        if (values.Length != skinJoints.Length)
                        {
                            throw new InvalidDataException("Inverse bind matrix count does not match the skin joints.");
                        }
                        for (int joint = 0; joint < skinJoints.Length; joint++)
                        {
                            inverseBind.Add(skinJoints[joint], Matrix(values[joint]));
                        }
                    }
                    else
                    {
                        foreach (int joint in skinJoints)
                        {
                            inverseBind.Add(joint, Matrix4x4.Identity);
                        }
                    }
                }
                foreach (JsonElement primitive in mesh.GetProperty("primitives").EnumerateArray())
                {
                    if (Int(primitive, "mode", 4) != 4 || primitive.TryGetProperty("targets", out _))
                    {
                        throw new InvalidDataException("Export triangles without morph targets.");
                    }
                    JsonElement attributes = primitive.GetProperty("attributes");
                    float[][] positions = Accessor(Int(attributes, "POSITION"), "VEC3", 5126);
                    totalVertices = checked(totalVertices + positions.Length);
                    if (totalVertices > 4_000_000)
                    {
                        throw new InvalidDataException("Too many model vertices.");
                    }
                    float[][]? normals = OptionalAccessor(attributes, "NORMAL", "VEC3", 5126);
                    float[][]? uvs = OptionalAccessor(attributes, "TEXCOORD_0", "VEC2");
                    float[][]? colors = OptionalAccessor(attributes, "COLOR_0");
                    float[][]? jointValues = OptionalAccessor(attributes, "JOINTS_0", "VEC4");
                    float[][]? weights = OptionalAccessor(attributes, "WEIGHTS_0", "VEC4");
                    if (attributes.TryGetProperty("JOINTS_0", out JsonElement jointAccessor))
                    {
                        JsonElement encoding = At("accessors", jointAccessor.GetInt32());
                        if (Int(encoding, "componentType") is not (5121 or 5123) || encoding.TryGetProperty("normalized", out JsonElement normalized) && normalized.GetBoolean())
                        {
                            throw new InvalidDataException("Joint indices must be unsigned byte or unsigned short values.");
                        }
                    }
                    if (attributes.TryGetProperty("JOINTS_1", out _) || attributes.TryGetProperty("WEIGHTS_1", out _))
                    {
                        throw new InvalidDataException("A maximum of four bone influences per vertex is supported.");
                    }
                    foreach (float[][]? values in new[] { normals, uvs, colors, jointValues, weights })
                    {
                        if (values is not null && values.Length != positions.Length)
                        {
                            throw new InvalidDataException("Vertex attribute counts do not agree.");
                        }
                    }
                    if (skinJoints.Length != 0 && (jointValues is null || weights is null))
                    {
                        throw new InvalidDataException("A skinned mesh needs joint indices and weights.");
                    }
                    int texture = -1;
                    Vector4 factor = Vector4.One;
                    bool transparent = false;
                    bool doubleSided = false;
                    if (primitive.TryGetProperty("material", out JsonElement materialReference))
                    {
                        JsonElement material = At("materials", materialReference.GetInt32());
                        transparent = String(material, "alphaMode", "OPAQUE") != "OPAQUE";
                        doubleSided = material.TryGetProperty("doubleSided", out JsonElement sided) && sided.GetBoolean();
                        if (material.TryGetProperty("pbrMetallicRoughness", out JsonElement pbr))
                        {
                            factor = Vec4(pbr, "baseColorFactor", Vector4.One);
                            if (pbr.TryGetProperty("baseColorTexture", out JsonElement colorTexture))
                            {
                                if (Int(colorTexture, "texCoord", 0) != 0 || colorTexture.TryGetProperty("extensions", out _))
                                {
                                    throw new InvalidDataException("Use UV map 0 and apply texture-coordinate transforms before export.");
                                }
                                texture = Int(colorTexture, "index");
                                if ((uint)texture >= textures.Count)
                                {
                                    throw new InvalidDataException("Material texture is out of range.");
                                }
                            }
                        }
                        if (material.TryGetProperty("normalTexture", out _) || material.TryGetProperty("emissiveTexture", out _) || material.TryGetProperty("occlusionTexture", out _))
                        {
                            _warnings.Add("Only base-color textures and vertex colors are used by the game renderer.");
                        }
                    }
                    List<EditableVertex> vertices = [];
                    for (int vertex = 0; vertex < positions.Length; vertex++)
                    {
                        Vector4 joint = Vector4.Zero;
                        Vector4 weight = weights is null ? Vector4.Zero : Vector(weights[vertex], 0);
                        if (skinJoints.Length != 0)
                        {
                            for (int slot = 0; slot < 4; slot++)
                            {
                                float index = jointValues![vertex][slot];
                                if (index != MathF.Truncate(index) || index < 0 || index >= skinJoints.Length)
                                {
                                    throw new InvalidDataException("A vertex refers to a joint outside its skin.");
                                }
                                joint[slot] = skinJoints[(int)index];
                            }
                            float sum = weight.X + weight.Y + weight.Z + weight.W;
                            if (sum <= 0 || MathF.Abs(sum - 1) > 0.02f)
                            {
                                throw new InvalidDataException("Invalid skin weight sum.");
                            }
                            weight /= sum;
                        }
                        Vector4 color = colors is null ? Vector4.One : Vector(colors[vertex], 1);
                        vertices.Add(new EditableVertex(V3(positions[vertex]), normals is null ? Vector3.UnitY : V3(normals[vertex]), uvs is null ? Vector2.Zero : new Vector2(uvs[vertex][0], uvs[vertex][1]), joint, weight, color * factor));
                    }
                    int[] indices;
                    if (primitive.TryGetProperty("indices", out JsonElement indexReference))
                    {
                        JsonElement encoding = At("accessors", indexReference.GetInt32());
                        if (Int(encoding, "componentType") is not (5121 or 5123 or 5125) || encoding.TryGetProperty("normalized", out JsonElement normalized) && normalized.GetBoolean())
                        {
                            throw new InvalidDataException("Triangle indices must be unsigned integers.");
                        }
                        float[][] values = Accessor(indexReference.GetInt32(), "SCALAR");
                        indices = values.Select(value => checked((int)value[0])).ToArray();
                    }
                    else
                    {
                        indices = Enumerable.Range(0, positions.Length).ToArray();
                    }
                    meshes.Add(new EditableMesh(String(mesh, "name", $"mesh_{meshes.Count}"), vertices, indices, texture, globals[nodeIndex]) { Node = skinJoints.Length == 0 ? nodeIndex : -1, Skinned = skinJoints.Length != 0, InverseBindMatrices = inverseBind, Transparent = transparent, DoubleSided = doubleSided });
                }
            }
            List<EditableAnimation> animations = [];
            if (root.TryGetProperty("animations", out JsonElement animationArray))
            {
                foreach (JsonElement animation in animationArray.EnumerateArray())
                {
                    List<EditableAnimationChannel> channels = [];
                    foreach (JsonElement channel in animation.GetProperty("channels").EnumerateArray())
                    {
                        JsonElement sampler = animation.GetProperty("samplers")[Int(channel, "sampler")];
                        JsonElement target = channel.GetProperty("target");
                        string path = String(target, "path", "");
                        if (path is not ("translation" or "rotation" or "scale"))
                        {
                            throw new InvalidDataException("Only skeletal translation, rotation and scale animations are supported.");
                        }
                        channels.Add(new EditableAnimationChannel(Int(target, "node"), path, Accessor(Int(sampler, "input"), "SCALAR", 5126).Select(value => value[0]).ToArray(), Accessor(Int(sampler, "output"), path == "rotation" ? "VEC4" : "VEC3", 5126).Select(value => Vector(value, 0)).ToArray(), String(sampler, "interpolation", "LINEAR")));
                    }
                    animations.Add(new EditableAnimation(String(animation, "name", $"animation_{animations.Count}"), channels));
                }
            }
            if (animations.Count != 0 && animations.All(animation => AnimationIndex(animation.Name) >= 0))
            {
                if (animations.Select(animation => AnimationIndex(animation.Name)).Distinct().Count() != animations.Count)
                {
                    throw new InvalidDataException("Animation identifiers are duplicated.");
                }
                animations = animations.OrderBy(animation => AnimationIndex(animation.Name)).ToList();
            }
            else if (original is not null && original.Animations.Any(animation => AnimationIndex(animation.Name) >= 0))
            {
                _warnings.Add("Animation names no longer identify the original game animation slots. Preserve animation_<index> names to keep gameplay animation identifiers compatible.");
            }
            EditableModel result = new(name, kind, bones, meshes, textures, animations, metadata);
            if (meshes.Count == 0)
            {
                throw new InvalidDataException("The GLB contains no triangle meshes.");
            }
            Validate(result);
            int oldCount = original?.Bones.Count(bone => bone.IsJoint) ?? joints.Count;
            bool mismatch = oldCount != joints.Count;
            if (mismatch)
            {
                _warnings.Add($"Bone count changed from {oldCount} to {joints.Count}. Existing game attachments and animations may require adjustment.");
            }
            if (original is not null && animations.Count < original.Animations.Count)
            {
                _warnings.Add($"Animation count changed from {original.Animations.Count} to {animations.Count}; game animation identifiers must remain compatible.");
            }
            if (original is not null && original.NativeMetadata.ValueKind == JsonValueKind.Object && (original.NativeMetadata.TryGetProperty("NativeParts", out JsonElement parts) && parts.ValueKind != JsonValueKind.Null || original.NativeMetadata.TryGetProperty("NativeEyes", out JsonElement eyes) && eyes.ValueKind != JsonValueKind.Null))
            {
                _warnings.Add("Original eye and model-part bindings are retained as metadata. Edited topology and skinning require compatible attachment authoring; native per-vertex skin translations are not applied to imported geometry.");
            }
            if (original is not null && original.Bones.Any(bone => bone.NativeIndex >= 0) && bones.Where(bone => bone.IsJoint).Any(bone => bone.NativeIndex < 0 && !bone.Name.StartsWith("bone_", StringComparison.Ordinal)))
            {
                _warnings.Add("Native attachment bone identifiers are missing on some joints. Preserve FF8_bone_index custom properties or the bone_<index> names for native-pose compatibility.");
            }
            return new ModelExchangeImportResult(result, _warnings.Distinct().ToArray(), mismatch && !allowBoneCountChange);
        }

        private List<PixelImage> ReadTextures()
        {
            List<PixelImage> result = [];
            long totalTexels = 0;
            if (!root.TryGetProperty("textures", out JsonElement textures))
            {
                return result;
            }
            if (textures.GetArrayLength() > 1024)
            {
                throw new InvalidDataException("Too many textures.");
            }
            foreach (JsonElement texture in textures.EnumerateArray())
            {
                JsonElement image = At("images", Int(texture, "source"));
                if (image.TryGetProperty("uri", out _) || !image.TryGetProperty("bufferView", out JsonElement viewReference) || String(image, "mimeType", "") is not ("image/png" or "image/jpeg"))
                {
                    throw new InvalidDataException("Embed PNG or JPEG textures in the GLB; external image paths are unsupported.");
                }
                byte[] content = ViewBytes(viewReference.GetInt32()).ToArray();
                bool png = content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                bool jpeg = content.Length >= 3 && content[0] == 255 && content[1] == 216 && content[2] == 255;
                if (!png && !jpeg)
                {
                    throw new InvalidDataException("Embedded image bytes must be PNG or JPEG.");
                }
                MagickImageInfo info = new(content);
                totalTexels += (long)info.Width * info.Height;
                if (info.Width == 0 || info.Height == 0 || info.Width > PixelImage.MaximumDimension || info.Height > PixelImage.MaximumDimension || (long)info.Width * info.Height > 16_777_216 || totalTexels > 67_108_864)
                {
                    throw new InvalidDataException("Texture dimensions exceed the supported image limit.");
                }
                using MagickImage decoded = new(content);
                using IPixelCollection<byte> pixels = decoded.GetPixels();
                byte[] rgba = pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidDataException("Texture pixels could not be decoded.");
                Rgba32[] colors = new Rgba32[rgba.Length / 4];
                for (int index = 0; index < colors.Length; index++)
                {
                    colors[index] = new Rgba32(rgba[index * 4], rgba[index * 4 + 1], rgba[index * 4 + 2], rgba[index * 4 + 3]);
                }
                result.Add(new PixelImage(checked((int)decoded.Width), checked((int)decoded.Height), colors));
            }
            return result;
        }

        private JsonElement At(string property, int index)
        {
            JsonElement array = root.GetProperty(property);
            if ((uint)index >= array.GetArrayLength())
            {
                throw new InvalidDataException($"A {property} reference is out of range.");
            }
            return array[index];
        }

        private ReadOnlySpan<byte> ViewBytes(int index)
        {
            JsonElement view = At("bufferViews", index);
            int offset = Int(view, "byteOffset", 0);
            int length = Int(view, "byteLength");
            if (Int(view, "buffer") != 0 || offset < 0 || length < 0 || (long)offset + length > Int(root.GetProperty("buffers")[0], "byteLength"))
            {
                throw new InvalidDataException("A buffer view exceeds the embedded buffer.");
            }
            return binary.AsSpan(offset, length);
        }

        private float[][]? OptionalAccessor(JsonElement attributes, string name, string? type = null, int component = 0)
        {
            return attributes.TryGetProperty(name, out JsonElement reference) ? Accessor(reference.GetInt32(), type, component) : null;
        }

        private float[][] Accessor(int index, string? expectedType = null, int expectedComponent = 0)
        {
            JsonElement accessor = At("accessors", index);
            string type = String(accessor, "type", "");
            int component = Int(accessor, "componentType");
            if (expectedType is not null && type != expectedType || expectedComponent != 0 && component != expectedComponent)
            {
                throw new InvalidDataException("Unexpected accessor type or component encoding.");
            }
            if (_accessorCache.TryGetValue(index, out float[][]? cached))
            {
                return cached;
            }
            int size = type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, _ => throw new InvalidDataException("Unsupported accessor shape.") };
            int width = component switch { 5120 or 5121 => 1, 5122 or 5123 => 2, 5125 or 5126 => 4, _ => throw new InvalidDataException("Unsupported accessor component.") };
            int count = Int(accessor, "count");
            if (count < 1 || count > 12_000_000 || (long)count * size > 48_000_000)
            {
                throw new InvalidDataException("Accessor count exceeds supported limits.");
            }
            bool normalized = accessor.TryGetProperty("normalized", out JsonElement normal) && normal.GetBoolean();
            if (normalized && component is 5125 or 5126)
            {
                throw new InvalidDataException("This component type cannot be normalized.");
            }
            float[][] values = Enumerable.Range(0, count).Select(_ => new float[size]).ToArray();
            if (accessor.TryGetProperty("bufferView", out JsonElement reference))
            {
                int viewIndex = reference.GetInt32();
                JsonElement view = At("bufferViews", viewIndex);
                int stride = Int(view, "byteStride", size * width);
                int start = Int(accessor, "byteOffset", 0);
                ReadOnlySpan<byte> bytes = ViewBytes(viewIndex);
                if (stride < size * width || stride % width != 0 || view.TryGetProperty("byteStride", out _) && (stride < 4 || stride > 252 || stride % 4 != 0) || start < 0 || start % width != 0 || (long)start + (long)(count - 1) * stride + size * width > bytes.Length)
                {
                    throw new InvalidDataException("Accessor offset, stride or extent is invalid.");
                }
                for (int row = 0; row < count; row++)
                {
                    for (int column = 0; column < size; column++)
                    {
                        values[row][column] = Component(bytes[(start + row * stride + column * width)..], component, normalized);
                    }
                }
            }
            if (accessor.TryGetProperty("sparse", out JsonElement sparse))
            {
                int sparseCount = Int(sparse, "count");
                JsonElement indices = sparse.GetProperty("indices");
                JsonElement replacements = sparse.GetProperty("values");
                int sparseType = Int(indices, "componentType");
                int sparseWidth = sparseType switch { 5121 => 1, 5123 => 2, 5125 => 4, _ => throw new InvalidDataException("Unsupported sparse index type.") };
                int indexStart = Int(indices, "byteOffset", 0);
                int valueStart = Int(replacements, "byteOffset", 0);
                ReadOnlySpan<byte> indexBytes = ViewBytes(Int(indices, "bufferView"));
                ReadOnlySpan<byte> valueBytes = ViewBytes(Int(replacements, "bufferView"));
                if (sparseCount < 1 || sparseCount > count || indexStart < 0 || valueStart < 0 || (long)indexStart + (long)sparseCount * sparseWidth > indexBytes.Length || (long)valueStart + (long)sparseCount * size * width > valueBytes.Length)
                {
                    throw new InvalidDataException("Sparse accessor exceeds its buffer view.");
                }
                int previous = -1;
                for (int row = 0; row < sparseCount; row++)
                {
                    int target = checked((int)Component(indexBytes[(indexStart + row * sparseWidth)..], sparseType, false));
                    if (target <= previous || target >= count)
                    {
                        throw new InvalidDataException("Sparse indices must be increasing and in range.");
                    }
                    previous = target;
                    for (int column = 0; column < size; column++)
                    {
                        values[target][column] = Component(valueBytes[(valueStart + (row * size + column) * width)..], component, normalized);
                    }
                }
            }
            _accessorCache.Add(index, values);
            return values;
        }

        private static float Component(ReadOnlySpan<byte> bytes, int type, bool normalized)
        {
            float result = type switch
            {
                5120 => normalized ? Math.Max((sbyte)bytes[0] / 127f, -1) : (sbyte)bytes[0],
                5121 => normalized ? bytes[0] / 255f : bytes[0],
                5122 => normalized ? Math.Max(BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32767f, -1) : BinaryPrimitives.ReadInt16LittleEndian(bytes),
                5123 => normalized ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) / 65535f : BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                5125 => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
                5126 => BinaryPrimitives.ReadSingleLittleEndian(bytes),
                _ => throw new InvalidDataException("Unsupported component type.")
            };
            if (!float.IsFinite(result))
            {
                throw new InvalidDataException("Accessor values must be finite.");
            }
            return result;
        }

        private static int Int(JsonElement element, string name, int? fallback = null) => element.TryGetProperty(name, out JsonElement value) ? value.GetInt32() : fallback ?? throw new InvalidDataException($"Missing {name}.");
        private static int AnimationIndex(string name) => name.StartsWith("animation_", StringComparison.Ordinal) && int.TryParse(name.AsSpan(10), out int index) && index >= 0 ? index : -1;
        private static string String(JsonElement element, string name, string fallback) => element.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? fallback : fallback;
        private static Vector3 V3(float[] values) => values.Length == 3 ? new Vector3(values[0], values[1], values[2]) : throw new InvalidDataException("Expected a three-component vector.");
        private static Vector4 Vector(float[] values, float w) => values.Length is 3 or 4 ? new Vector4(values[0], values[1], values[2], values.Length == 4 ? values[3] : w) : throw new InvalidDataException("Expected a three- or four-component vector.");
        private static Vector3 Vec3(JsonElement element, string name, Vector3 fallback) => element.TryGetProperty(name, out JsonElement value) ? V3(value.EnumerateArray().Select(component => component.GetSingle()).ToArray()) : fallback;
        private static Vector4 Vec4(JsonElement element, string name, Vector4 fallback) => element.TryGetProperty(name, out JsonElement value) ? Vector(value.EnumerateArray().Select(component => component.GetSingle()).ToArray(), 1) : fallback;
        private static Matrix4x4 Matrix(float[] values) => values.Length == 16 ? new Matrix4x4(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15]) : throw new InvalidDataException("Expected a sixteen-component matrix.");
    }
}
