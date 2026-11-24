using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Assets;

public static partial class ModelExchange
{
    public static FieldSpringBoneAsset? ReadFieldSpringBoneAsset(ReadOnlySpan<byte> content)
    {
        (byte[] json, _) = ReadGlbChunks(content, readBinary: false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 128 });
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("extensions", out JsonElement extensions) || !extensions.TryGetProperty(VrmSpringBones.ExtensionName, out _))
            {
                return null;
            }
            JsonElement nodes = root.GetProperty("nodes");
            if (nodes.GetArrayLength() > 4096 || root.GetProperty("asset").GetProperty("version").GetString() != "2.0")
            {
                throw new InvalidDataException("Spring bones require glTF 2.0 with at most 4096 nodes.");
            }
            int[] parents = Enumerable.Repeat(-1, nodes.GetArrayLength()).ToArray();
            ModelImportTarget? destination = ReadImportTarget(root.GetProperty("asset"), null);
            string? manifest = ReadMetadataString(root.GetProperty("asset"), "memoria_ff8", null);
            bool authoringBasis = false;
            for (int index = 0; index < parents.Length; index++)
            {
                JsonElement node = nodes[index];
                destination = ReadImportTarget(node, destination);
                manifest = ReadMetadataString(node, "memoria_ff8", manifest);
                authoringBasis |= node.TryGetProperty("extras", out JsonElement extras) && extras.ValueKind == JsonValueKind.Object && extras.TryGetProperty("memoria_basis", out JsonElement basis) && basis.GetInt32() == 2;
                if (node.TryGetProperty("children", out JsonElement children))
                {
                    foreach (JsonElement child in children.EnumerateArray())
                    {
                        int target = child.GetInt32();
                        if ((uint)target >= parents.Length || parents[target] >= 0 || target == index)
                        {
                            throw new InvalidDataException("Invalid or multiply parented spring bone node.");
                        }
                        parents[target] = index;
                    }
                }
            }
            EditableBone[] bones = new EditableBone[parents.Length];
            for (int index = 0; index < bones.Length; index++)
            {
                JsonElement node = nodes[index];
                (Vector3 position, Quaternion rotation, Vector3 scale) = GlbReader.ReadNodeTransform(node);
                string name = node.TryGetProperty("name", out JsonElement nameValue) ? nameValue.GetString() ?? "" : "";
                int native = node.TryGetProperty("extras", out JsonElement extras) && extras.ValueKind == JsonValueKind.Object && extras.TryGetProperty("FF8_bone_index", out JsonElement id) ? id.GetInt32() : -1;
                bones[index] = new(name, parents[index], position, rotation, scale) { NativeIndex = native };
            }
            VrmSpringBones springs = VrmSpringBones.Read(root, parents)!;
            EditableModel rig = new("Spring rig", "field", bones, [], [], [], default) { ExchangeManifest = manifest };
            Validate(rig);
            if (authoringBasis)
            {
                rig = ChangeAuthoringBasis(rig, importing: true);
            }
            bool hasModelContent = root.TryGetProperty("meshes", out JsonElement meshes) && meshes.GetArrayLength() != 0 || root.TryGetProperty("animations", out JsonElement animations) && animations.GetArrayLength() != 0;
            return new(rig.Bones, springs, destination, authoringBasis, hasModelContent);
        }
        catch (Exception exception) when (IsGlbStructureException(exception))
        {
            throw new InvalidDataException("The GLB contains invalid spring bone structure.", exception);
        }
    }

    public static byte[] ExportFieldSpringBoneAsset(FieldSpringBoneAsset asset)
    {
        IReadOnlyList<EditableBone> nodes = asset.Nodes;
        if (asset.MemoriaAuthoringBasis)
        {
            nodes = ChangeAuthoringBasis(new("Spring rig", "field", nodes, [], [], [], default)).Bones;
        }
        var children = Enumerable.Range(0, nodes.Count).ToLookup(index => nodes[index].Parent);
        using MemoryStream json = new();
        using (Utf8JsonWriter writer = new(json))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("asset");
            writer.WriteString("version", "2.0");
            writer.WriteEndObject();
            writer.WriteNumber("scene", 0);
            writer.WriteStartArray("scenes");
            writer.WriteStartObject();
            writer.WritePropertyName("nodes");
            JsonSerializer.Serialize(writer, children[-1].ToArray());
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteStartArray("nodes");
            for (int index = 0; index < nodes.Count; index++)
            {
                EditableBone bone = nodes[index];
                var node = TransformNode(bone.Name, bone.Translation, bone.Rotation, bone.Scale);
                if (children.Contains(index))
                {
                    node["children"] = JsonSerializer.SerializeToNode(children[index].ToArray());
                }
                if (bone.NativeIndex >= 0 || index == 0 && asset.MemoriaAuthoringBasis)
                {
                    var extras = new System.Text.Json.Nodes.JsonObject();
                    if (bone.NativeIndex >= 0)
                    {
                        extras["FF8_bone_index"] = bone.NativeIndex;
                    }
                    if (index == 0 && asset.MemoriaAuthoringBasis)
                    {
                        extras["memoria_basis"] = 2;
                    }
                    node["extras"] = extras;
                }
                node.WriteTo(writer);
            }
            writer.WriteEndArray();
            writer.WriteStartArray("extensionsUsed");
            writer.WriteStringValue(VrmSpringBones.ExtensionName);
            writer.WriteEndArray();
            writer.WriteStartObject("extensions");
            asset.Springs.Write(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        int length = checked((int)json.Length);
        int aligned = (length + 3) & ~3;
        using MemoryStream output = new();
        using BinaryWriter container = new(output);
        container.Write(0x46546c67u);
        container.Write(2u);
        container.Write(20 + aligned);
        container.Write(aligned);
        container.Write(0x4e4f534au);
        json.WriteTo(output);
        for (int index = length; index < aligned; index++)
        {
            container.Write((byte)32);
        }
        return output.ToArray();
    }
}

public sealed record FieldSpringBoneAsset(IReadOnlyList<EditableBone> Nodes, VrmSpringBones Springs, ModelImportTarget? Destination, bool MemoriaAuthoringBasis, bool HasModelContent)
{
    public IReadOnlyList<FieldSpringBoneJoint> Bind(FieldModel model)
    {
        IReadOnlyList<EditableBone>? edited = model.EditableOverride?.Bones;
        int[] parents = model.GetSpringBoneParents();
        Dictionary<string, int[]> names = Enumerable.Range(0, parents.Length).GroupBy(index => edited?[index].Name ?? $"bone_{index}", StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        Dictionary<int, int[]> native = Enumerable.Range(0, parents.Length).GroupBy(index => edited?[index].NativeIndex ?? index).ToDictionary(group => group.Key, group => group.ToArray());
        int[] map = Nodes.Select(node =>
        {
            int[]? matches = node.NativeIndex >= 0 ? native.GetValueOrDefault(node.NativeIndex) : null;
            matches ??= names.GetValueOrDefault(node.Name);
            return matches is { Length: 1 } ? matches[0] : -1;
        }).ToArray();
        List<FieldSpringBoneJoint> joints = [];
        HashSet<int> used = [];
        foreach (VrmSpring spring in Springs.Springs)
        {
            if (spring.Joints.Count < 2)
            {
                continue;
            }
            int? center = spring.Center is int centerNode ? RequireMapped(centerNode) : null;
            FieldSpringBoneCollider[] colliders = spring.ColliderGroups.SelectMany(group => Springs.ColliderGroups[group].Colliders).Distinct().Select(index => BindCollider(Springs.Colliders[index])).ToArray();
            for (int index = 0; index + 1 < spring.Joints.Count; index++)
            {
                VrmSpringJoint joint = spring.Joints[index];
                int bone = RequireMapped(joint.Node);
                int tailNode = spring.Joints[index + 1].Node;
                if (!used.Add(bone) || map[tailNode] >= 0 && !IsAncestor(bone, map[tailNode], parents))
                {
                    throw new InvalidDataException($"Spring joint {Nodes[joint.Node].Name} duplicates a field bone or changes its hierarchy.");
                }
                if (center is int centerBone && centerBone != bone && !IsAncestor(centerBone, bone, parents))
                {
                    throw new InvalidDataException($"Spring center for {Nodes[joint.Node].Name} is outside the field bone hierarchy.");
                }
                Vector3 tail = Relative(tailNode, joint.Node).Translation;
                joints.Add(new(bone, ModelExchange.ToNative(tail), joint.Stiffness, joint.DragForce, ModelExchange.ToNative(joint.GravityDir * joint.GravityPower))
                {
                    Center = center,
                    HitRadius = joint.HitRadius,
                    Colliders = colliders
                });
            }
        }
        _ = new FieldSpringBoneSimulation(parents, joints);
        return Array.AsReadOnly(joints.ToArray());

        int RequireMapped(int node)
        {
            int target = map[node];
            if (target < 0)
            {
                throw new InvalidDataException($"Spring bone node '{Nodes[node].Name}' does not identify a field bone. Preserve its exported name or native index.");
            }
            for (int ancestor = Nodes[node].Parent; ancestor >= 0; ancestor = Nodes[ancestor].Parent)
            {
                if (map[ancestor] >= 0)
                {
                    if (!IsAncestor(map[ancestor], target, parents))
                    {
                        throw new InvalidDataException($"Spring bone '{Nodes[node].Name}' changes the selected field rig hierarchy.");
                    }
                    break;
                }
            }
            return target;
        }

        FieldSpringBoneCollider BindCollider(VrmSpringCollider collider)
        {
            int anchor = collider.Node;
            while (anchor >= 0 && map[anchor] < 0)
            {
                anchor = Nodes[anchor].Parent;
            }
            if (anchor < 0)
            {
                throw new InvalidDataException("A spring collider must attach to a field bone or its child node.");
            }
            int bone = RequireMapped(anchor);
            Matrix4x4 local = Relative(collider.Node, anchor);
            if (!Matrix4x4.Decompose(local, out Vector3 scale, out _, out _) || scale.X <= 0 || MathF.Abs(scale.X - scale.Y) > 0.0001f || MathF.Abs(scale.X - scale.Z) > 0.0001f)
            {
                throw new InvalidDataException("Spring colliders require uniformly scaled helper nodes.");
            }
            Vector3 Point(Vector3 value)
            {
                if (MemoriaAuthoringBasis)
                {
                    value *= new Vector3(1, -1, -1);
                }
                return ModelExchange.ToNative(Vector3.Transform(value, local));
            }
            return new(bone, Point(collider.Offset), collider.Radius * scale.X, collider.Tail is Vector3 tip ? Point(tip) : null);
        }
    }

    private Matrix4x4 Relative(int node, int ancestor)
    {
        Matrix4x4 result = Matrix4x4.Identity;
        while (node != ancestor)
        {
            if (node < 0)
            {
                throw new InvalidDataException("A spring segment must end at a descendant node.");
            }
            EditableBone bone = Nodes[node];
            result *= Matrix4x4.CreateScale(bone.Scale) * Matrix4x4.CreateFromQuaternion(bone.Rotation) * Matrix4x4.CreateTranslation(bone.Translation);
            node = bone.Parent;
        }
        return result;
    }

    private static bool IsAncestor(int ancestor, int node, IReadOnlyList<int> parents)
    {
        for (int parent = parents[node]; parent >= 0; parent = parents[parent])
        {
            if (parent == ancestor)
            {
                return true;
            }
        }
        return false;
    }
}
