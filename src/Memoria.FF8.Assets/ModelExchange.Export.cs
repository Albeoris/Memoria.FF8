using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

public static partial class ModelExchange
{
    public static byte[] ExportGlb(EditableModel model)
    {
        Validate(model);
        using MemoryStream binary = new();
        using BinaryWriter writer = new(binary, Encoding.UTF8, true);
        JsonArray views = [];
        JsonArray accessors = [];
        JsonArray nodes = [];
        JsonArray meshes = [];
        JsonArray skins = [];
        JsonArray materials = [];
        JsonArray images = [];
        JsonArray textures = [];
        JsonArray animations = [];
        int View(byte[] data)
        {
            while (binary.Position % 4 != 0)
            {
                writer.Write((byte)0);
            }
            int result = views.Count;
            views.Add(new JsonObject { ["buffer"] = 0, ["byteOffset"] = checked((int)binary.Position), ["byteLength"] = data.Length });
            writer.Write(data);
            return result;
        }
        int Accessor(float[] values, int size, bool bounds = false)
        {
            int view = View(MemoryMarshal.AsBytes(values.AsSpan()).ToArray());
            JsonObject accessor = new() { ["bufferView"] = view, ["componentType"] = 5126, ["count"] = values.Length / size, ["type"] = size == 1 ? "SCALAR" : size == 16 ? "MAT4" : $"VEC{size}" };
            if (bounds)
            {
                accessor["min"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, size).Select(component => values.Where((_, index) => index % size == component).Min()).ToArray());
                accessor["max"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, size).Select(component => values.Where((_, index) => index % size == component).Max()).ToArray());
            }
            int result = accessors.Count;
            accessors.Add(accessor);
            return result;
        }
        int IntegerAccessor(int[] values, int size, bool joints)
        {
            byte[] data = new byte[values.Length * (joints ? 2 : 4)];
            for (int index = 0; index < values.Length; index++)
            {
                if (joints)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(index * 2), checked((ushort)values[index]));
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), checked((uint)values[index]));
                }
            }
            int result = accessors.Count;
            accessors.Add(new JsonObject { ["bufferView"] = View(data), ["componentType"] = joints ? 5123 : 5125, ["count"] = values.Length / size, ["type"] = size == 1 ? "SCALAR" : $"VEC{size}" });
            return result;
        }
        for (int index = 0; index < model.Bones.Count; index++)
        {
            EditableBone bone = model.Bones[index];
            JsonObject node = TransformNode(bone.Name, bone.Translation, bone.Rotation, bone.Scale);
            if (bone.NativeIndex >= 0)
            {
                node["extras"] = new JsonObject { ["FF8_bone_index"] = bone.NativeIndex };
            }
            int[] children = Enumerable.Range(0, model.Bones.Count).Where(child => model.Bones[child].Parent == index).ToArray();
            if (children.Length != 0)
            {
                node["children"] = JsonSerializer.SerializeToNode(children);
            }
            nodes.Add(node);
        }
        for (int index = 0; index < model.Textures.Count; index++)
        {
            images.Add(new JsonObject { ["name"] = $"texture_{index}", ["bufferView"] = View(EncodePng(model.Textures[index])), ["mimeType"] = "image/png" });
            textures.Add(new JsonObject { ["source"] = index, ["sampler"] = 0 });
        }
        Matrix4x4[] global = Globals(model.Bones);
        int[] jointBones = Enumerable.Range(0, model.Bones.Count).Where(index => model.Bones[index].IsJoint).ToArray();
        Dictionary<int, int> jointSlots = jointBones.Select((bone, slot) => (bone, slot)).ToDictionary(value => value.bone, value => value.slot);
        List<int> roots = Enumerable.Range(0, model.Bones.Count).Where(index => model.Bones[index].Parent < 0).ToList();
        foreach (EditableMesh mesh in model.Meshes)
        {
            JsonObject attributes = new()
            {
                ["POSITION"] = Accessor(mesh.Vertices.SelectMany(vertex => new[] { vertex.Position.X, vertex.Position.Y, vertex.Position.Z }).ToArray(), 3, true),
                ["NORMAL"] = Accessor(mesh.Vertices.SelectMany(vertex => new[] { vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z }).ToArray(), 3),
                ["TEXCOORD_0"] = Accessor(mesh.Vertices.SelectMany(vertex => new[] { vertex.Uv.X, vertex.Uv.Y }).ToArray(), 2),
                ["COLOR_0"] = Accessor(mesh.Vertices.SelectMany(vertex => new[] { vertex.Color.X, vertex.Color.Y, vertex.Color.Z, vertex.Color.W }).ToArray(), 4)
            };
            JsonObject material = new()
            {
                ["name"] = $"material_{materials.Count}",
                ["doubleSided"] = mesh.DoubleSided,
                ["alphaMode"] = mesh.Transparent || mesh.Vertices.Any(vertex => vertex.Color.W < 1) || mesh.Texture >= 0 && model.Textures[mesh.Texture].Pixels.Any(pixel => pixel.A < 255) ? "BLEND" : "OPAQUE",
                ["pbrMetallicRoughness"] = new JsonObject { ["metallicFactor"] = 0, ["roughnessFactor"] = 1 }
            };
            if (mesh.Texture >= 0)
            {
                material["pbrMetallicRoughness"]!["baseColorTexture"] = new JsonObject { ["index"] = mesh.Texture };
            }
            int materialIndex = materials.Count;
            materials.Add(material);
            JsonObject node = new() { ["name"] = mesh.Name, ["mesh"] = meshes.Count };
            if (mesh.Skinned)
            {
                attributes["JOINTS_0"] = IntegerAccessor(mesh.Vertices.SelectMany(vertex => Enumerable.Range(0, 4).Select(slot => vertex.Weights[slot] == 0 ? 0 : jointSlots[(int)vertex.Joints[slot]])).ToArray(), 4, true);
                attributes["WEIGHTS_0"] = Accessor(mesh.Vertices.SelectMany(vertex => new[] { vertex.Weights.X, vertex.Weights.Y, vertex.Weights.Z, vertex.Weights.W }).ToArray(), 4);
                float[] matrices = jointBones.SelectMany(bone =>
                {
                    Matrix4x4 inverse = mesh.InverseBindMatrices is not null && mesh.InverseBindMatrices.TryGetValue(bone, out Matrix4x4 supplied) ? supplied : Matrix4x4.Invert(global[bone], out Matrix4x4 calculated) ? calculated : throw new InvalidDataException("A bind transform is singular.");
                    return MatrixValues(inverse);
                }).ToArray();
                node["skin"] = skins.Count;
                skins.Add(new JsonObject { ["joints"] = JsonSerializer.SerializeToNode(jointBones), ["inverseBindMatrices"] = Accessor(matrices, 16) });
            }
            else
            {
                node["matrix"] = JsonSerializer.SerializeToNode(MatrixValues(mesh.Node >= 0 ? Matrix4x4.Identity : mesh.Transform));
            }
            meshes.Add(new JsonObject { ["name"] = mesh.Name, ["primitives"] = new JsonArray(new JsonObject { ["attributes"] = attributes, ["indices"] = IntegerAccessor(mesh.Indices.ToArray(), 1, false), ["material"] = materialIndex, ["mode"] = 4 }) });
            if (!mesh.Skinned && mesh.Node >= 0)
            {
                JsonObject parent = (JsonObject)nodes[mesh.Node]!;
                JsonArray children = parent["children"] as JsonArray ?? [];
                if (parent["children"] is null)
                {
                    parent["children"] = children;
                }
                children.Add(nodes.Count);
            }
            else
            {
                roots.Add(nodes.Count);
            }
            nodes.Add(node);
        }
        foreach (EditableAnimation animation in model.Animations.Where(animation => animation.Channels.Count != 0))
        {
            JsonArray samplers = [];
            JsonArray channels = [];
            foreach (EditableAnimationChannel channel in animation.Channels)
            {
                int size = channel.Path == "rotation" ? 4 : 3;
                channels.Add(new JsonObject { ["sampler"] = samplers.Count, ["target"] = new JsonObject { ["node"] = channel.Bone, ["path"] = channel.Path } });
                samplers.Add(new JsonObject { ["input"] = Accessor(channel.Times, 1, true), ["output"] = Accessor(channel.Values.SelectMany(value => Enumerable.Range(0, size).Select(index => value[index])).ToArray(), size), ["interpolation"] = channel.Interpolation });
            }
            animations.Add(new JsonObject { ["name"] = animation.Name, ["channels"] = channels, ["samplers"] = samplers });
        }
        // A string custom property survives Blender's import/export custom-property option.
        string metadata = model.NativeMetadata.ValueKind == JsonValueKind.Undefined ? "{}" : model.NativeMetadata.GetRawText();
        int metadataNode = nodes.Count;
        nodes.Add(new JsonObject { ["name"] = model.Name, ["extras"] = new JsonObject { ["FF8_kind"] = model.Kind, ["FF8_native"] = metadata, ["FF8_schema"] = 1 } });
        roots.Add(metadataNode);
        while (binary.Position % 4 != 0)
        {
            writer.Write((byte)0);
        }
        JsonObject document = new()
        {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "Memoria FF8 model exchange" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["name"] = model.Name, ["nodes"] = JsonSerializer.SerializeToNode(roots) }),
            ["nodes"] = nodes,
            ["meshes"] = meshes,
            ["bufferViews"] = views,
            ["accessors"] = accessors,
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = checked((int)binary.Length) })
        };
        if (skins.Count != 0)
        {
            document["skins"] = skins;
        }
        if (materials.Count != 0)
        {
            document["materials"] = materials;
        }
        if (images.Count != 0)
        {
            document["images"] = images;
            document["textures"] = textures;
            document["samplers"] = new JsonArray(new JsonObject { ["magFilter"] = 9728, ["minFilter"] = 9728, ["wrapS"] = 10497, ["wrapT"] = 10497 });
        }
        if (animations.Count != 0)
        {
            document["animations"] = animations;
        }
        byte[] json = Encoding.UTF8.GetBytes(document.ToJsonString());
        int jsonLength = (json.Length + 3) & ~3;
        using MemoryStream output = new();
        using BinaryWriter container = new(output);
        container.Write(0x46546c67u);
        container.Write(2u);
        container.Write(checked((uint)(12 + 8 + jsonLength + 8 + binary.Length)));
        container.Write(jsonLength);
        container.Write(0x4e4f534au);
        container.Write(json);
        for (int index = json.Length; index < jsonLength; index++)
        {
            container.Write((byte)32);
        }
        container.Write(checked((int)binary.Length));
        container.Write(0x004e4942u);
        container.Write(binary.ToArray());
        return output.ToArray();
    }

    private static JsonObject TransformNode(string name, Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        return new JsonObject { ["name"] = name, ["translation"] = JsonSerializer.SerializeToNode(new[] { translation.X, translation.Y, translation.Z }), ["rotation"] = JsonSerializer.SerializeToNode(new[] { rotation.X, rotation.Y, rotation.Z, rotation.W }), ["scale"] = JsonSerializer.SerializeToNode(new[] { scale.X, scale.Y, scale.Z }) };
    }

    private static float[] MatrixValues(Matrix4x4 value) => [value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24, value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44];

    private static byte[] EncodePng(PixelImage image)
    {
        using MemoryStream output = new();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        void Chunk(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            Span<byte> header = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(header, data.Length);
            output.Write(header);
            output.Write(type);
            output.Write(data);
            uint crc = uint.MaxValue;
            foreach (byte value in type.ToArray().Concat(data.ToArray()))
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
                }
            }
            BinaryPrimitives.WriteUInt32BigEndian(header, ~crc);
            output.Write(header);
        }
        byte[] dimensions = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(dimensions, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(dimensions.AsSpan(4), image.Height);
        dimensions[8] = 8;
        dimensions[9] = 6;
        Chunk("IHDR"u8, dimensions);
        using MemoryStream compressed = new();
        using (ZLibStream compressor = new(compressed, CompressionLevel.Fastest, true))
        {
            ReadOnlySpan<byte> pixels = MemoryMarshal.AsBytes(image.Pixels.AsSpan());
            for (int row = 0; row < image.Height; row++)
            {
                compressor.WriteByte(0);
                compressor.Write(pixels.Slice(row * image.Width * 4, image.Width * 4));
            }
        }
        Chunk("IDAT"u8, compressed.ToArray());
        Chunk("IEND"u8, []);
        return output.ToArray();
    }
}
