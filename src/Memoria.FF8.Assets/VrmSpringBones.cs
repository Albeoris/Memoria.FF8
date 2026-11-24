using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Assets;

public sealed record VrmSpringCollider(int Node, Vector3 Offset, float Radius, Vector3? Tail = null);

public sealed record VrmSpringColliderGroup(string? Name, IReadOnlyList<int> Colliders);

public sealed record VrmSpringJoint(int Node, float HitRadius, float Stiffness, float GravityPower, Vector3 GravityDir, float DragForce);

public sealed record VrmSpring(string? Name, IReadOnlyList<VrmSpringJoint> Joints, IReadOnlyList<int> ColliderGroups, int? Center = null);

public sealed record VrmSpringBones(IReadOnlyList<VrmSpringCollider> Colliders, IReadOnlyList<VrmSpringColliderGroup> ColliderGroups, IReadOnlyList<VrmSpring> Springs)
{
    public const string ExtensionName = "VRMC_springBone";
    private const string SpecVersion = "1.0";

    public static VrmSpringBones? Read(JsonElement gltf, IReadOnlyList<int> parentIndices)
    {
        if (!AsObject(gltf, "glTF root").TryGetProperty("extensions", out JsonElement extensions) ||
            !AsObject(extensions, "glTF extensions").TryGetProperty(ExtensionName, out JsonElement value))
        {
            return null;
        }

        JsonElement extension = AsObject(value, ExtensionName);
        string? version = ReadString(extension, "specVersion");
        if (version != SpecVersion)
        {
            throw new InvalidDataException($"{ExtensionName} requires specVersion {SpecVersion}.");
        }

        VrmSpringCollider[] colliders = ReadArray(extension, "colliders", collider => ReadCollider(collider, parentIndices.Count));
        VrmSpringColliderGroup[] groups = ReadArray(extension, "colliderGroups", group => ReadColliderGroup(group, colliders.Length));
        VrmSpring[] springs = ReadArray(extension, "springs", spring => ReadSpring(spring, parentIndices.Count, groups.Length));
        ValidateSpringHierarchy(springs, parentIndices);
        return new VrmSpringBones(colliders, groups, springs);
    }

    public void Write(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(ExtensionName);
        writer.WriteString("specVersion", SpecVersion);
        WriteArray(writer, "colliders", Colliders, WriteCollider);
        WriteArray(writer, "colliderGroups", ColliderGroups, WriteColliderGroup);
        WriteArray(writer, "springs", Springs, WriteSpring);
        writer.WriteEndObject();
    }

    private static VrmSpringCollider ReadCollider(JsonElement value, int nodeCount)
    {
        JsonElement collider = AsObject(value, "collider");
        int node = ReadIndex(ReadRequired(collider, "node"), nodeCount, "collider node");
        JsonElement shape = AsObject(ReadRequired(collider, "shape"), "collider shape");
        bool hasSphere = shape.TryGetProperty("sphere", out JsonElement sphere);
        bool hasCapsule = shape.TryGetProperty("capsule", out JsonElement capsule);
        if (hasSphere == hasCapsule)
        {
            throw new InvalidDataException($"{ExtensionName} collider shape must specify exactly one sphere or capsule.");
        }

        JsonElement geometry = AsObject(hasCapsule ? capsule : sphere, "collider geometry");
        Vector3 offset = ReadVector(geometry, "offset", Vector3.Zero);
        float radius = ReadNumber(geometry, "radius", 0f);
        Vector3? tail = hasCapsule ? ReadVector(geometry, "tail", Vector3.Zero) : null;
        return new VrmSpringCollider(node, offset, radius, tail);
    }

    private static VrmSpringColliderGroup ReadColliderGroup(JsonElement value, int colliderCount)
    {
        JsonElement group = AsObject(value, "collider group");
        int[] colliders = ReadArray(group, "colliders", index => ReadIndex(index, colliderCount, "collider index"), required: true);
        return new VrmSpringColliderGroup(ReadString(group, "name"), colliders);
    }

    private static VrmSpring ReadSpring(JsonElement value, int nodeCount, int groupCount)
    {
        JsonElement spring = AsObject(value, "spring");
        VrmSpringJoint[] joints = ReadArray(spring, "joints", joint => ReadJoint(joint, nodeCount), required: true);
        int[] groups = ReadArray(spring, "colliderGroups", index => ReadIndex(index, groupCount, "collider group index"));
        int? center = spring.TryGetProperty("center", out JsonElement centerValue) ? ReadIndex(centerValue, nodeCount, "spring center node") : null;
        return new VrmSpring(ReadString(spring, "name"), joints, groups, center);
    }

    private static VrmSpringJoint ReadJoint(JsonElement value, int nodeCount)
    {
        JsonElement joint = AsObject(value, "spring joint");
        int node = ReadIndex(ReadRequired(joint, "node"), nodeCount, "spring joint node");
        float hitRadius = ReadNumber(joint, "hitRadius", 0f);
        float stiffness = ReadNumber(joint, "stiffness", 1f);
        float gravityPower = ReadNumber(joint, "gravityPower", 0f);
        Vector3 gravityDir = ReadVector(joint, "gravityDir", -Vector3.UnitY);
        float dragForce = ReadNumber(joint, "dragForce", 0.5f, 1f);
        return new VrmSpringJoint(node, hitRadius, stiffness, gravityPower, gravityDir, dragForce);
    }

    private static void ValidateSpringHierarchy(IReadOnlyList<VrmSpring> springs, IReadOnlyList<int> parents)
    {
        if (springs.Count == 0)
        {
            return;
        }

        ValidateParents(parents);
        var owner = new VrmSpring?[parents.Count];
        foreach (VrmSpring spring in springs)
        {
            int node = spring.Joints[^1].Node;
            for (int jointIndex = spring.Joints.Count - 1; jointIndex >= 0; jointIndex--)
            {
                int joint = spring.Joints[jointIndex].Node;
                while (node != joint)
                {
                    if (node == -1)
                    {
                        throw new InvalidDataException($"{ExtensionName} joint {joint} must be an ancestor of joint {spring.Joints[jointIndex + 1].Node}.");
                    }

                    Claim(node);
                    node = parents[node];
                }

                Claim(joint);
                node = parents[node];
            }

            void Claim(int index)
            {
                if (owner[index] is not null)
                {
                    throw new InvalidDataException($"{ExtensionName} node {index} belongs to more than one joint or spring.");
                }

                owner[index] = spring;
            }
        }

        foreach (VrmSpring spring in springs)
        {
            if (spring.Center is not int center)
            {
                continue;
            }

            int node = spring.Joints[0].Node;
            while (node != -1 && node != center)
            {
                node = parents[node];
            }

            if (node == -1)
            {
                throw new InvalidDataException($"{ExtensionName} center node {center} must be the first joint or one of its ancestors.");
            }

            for (node = center; node != -1; node = parents[node])
            {
                if (owner[node] is VrmSpring other && !ReferenceEquals(other, spring))
                {
                    throw new InvalidDataException($"{ExtensionName} center node {center} cannot be in or below another spring.");
                }
            }
        }
    }

    private static void ValidateParents(IReadOnlyList<int> parents)
    {
        var visited = new int[parents.Count];
        for (int index = 0; index < parents.Count; index++)
        {
            for (int node = index; node != -1; node = parents[node])
            {
                if ((uint)node >= (uint)parents.Count || visited[node] == index + 1)
                {
                    throw new InvalidDataException($"{ExtensionName} requires a valid glTF node hierarchy.");
                }

                if (visited[node] != 0)
                {
                    break;
                }

                visited[node] = index + 1;
            }
        }
    }

    private static JsonElement AsObject(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{ExtensionName} expected an object for {description}.");
        }

        return value;
    }

    private static JsonElement ReadRequired(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement result))
        {
            throw new InvalidDataException($"{ExtensionName} requires {property}.");
        }

        return result;
    }

    private static T[] ReadArray<T>(JsonElement value, string property, Func<JsonElement, T> readElement, bool required = false)
    {
        if (!value.TryGetProperty(property, out JsonElement array))
        {
            if (required)
            {
                throw new InvalidDataException($"{ExtensionName} requires {property}.");
            }

            return [];
        }

        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
        {
            throw new InvalidDataException($"{ExtensionName} {property} must be a nonempty array.");
        }

        return array.EnumerateArray().Select(readElement).ToArray();
    }

    private static string? ReadString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement name))
        {
            return null;
        }

        if (name.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{ExtensionName} {property} must be a string.");
        }

        return name.GetString();
    }

    private static int ReadIndex(JsonElement value, int count, string description)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int index) || (uint)index >= (uint)count)
        {
            throw new InvalidDataException($"{ExtensionName} {description} is out of range.");
        }

        return index;
    }

    private static float ReadNumber(JsonElement value, string property, float defaultValue, float maximum = float.MaxValue)
    {
        if (!value.TryGetProperty(property, out JsonElement number))
        {
            return defaultValue;
        }

        float result = ReadFiniteNumber(number, property);
        if (result < 0f || result > maximum)
        {
            throw new InvalidDataException($"{ExtensionName} {property} is out of range.");
        }

        return result;
    }

    private static Vector3 ReadVector(JsonElement value, string property, Vector3 defaultValue)
    {
        if (!value.TryGetProperty(property, out JsonElement array))
        {
            return defaultValue;
        }

        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 3)
        {
            throw new InvalidDataException($"{ExtensionName} {property} must contain three coordinates.");
        }

        return new Vector3(ReadFiniteNumber(array[0], property), ReadFiniteNumber(array[1], property), ReadFiniteNumber(array[2], property));
    }

    private static float ReadFiniteNumber(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out float number) || !float.IsFinite(number))
        {
            throw new InvalidDataException($"{ExtensionName} {description} must be a finite number.");
        }

        return number;
    }

    private static void WriteCollider(Utf8JsonWriter writer, VrmSpringCollider collider)
    {
        writer.WriteStartObject();
        writer.WriteNumber("node", collider.Node);
        writer.WriteStartObject("shape");
        writer.WriteStartObject(collider.Tail.HasValue ? "capsule" : "sphere");
        WriteVector(writer, "offset", collider.Offset);
        writer.WriteNumber("radius", collider.Radius);
        if (collider.Tail is Vector3 tail)
        {
            WriteVector(writer, "tail", tail);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteColliderGroup(Utf8JsonWriter writer, VrmSpringColliderGroup group)
    {
        writer.WriteStartObject();
        WriteName(writer, group.Name);
        WriteArray(writer, "colliders", group.Colliders, (json, index) => json.WriteNumberValue(index));
        writer.WriteEndObject();
    }

    private static void WriteSpring(Utf8JsonWriter writer, VrmSpring spring)
    {
        writer.WriteStartObject();
        WriteName(writer, spring.Name);
        WriteArray(writer, "joints", spring.Joints, WriteJoint);
        WriteArray(writer, "colliderGroups", spring.ColliderGroups, (json, index) => json.WriteNumberValue(index));
        if (spring.Center is int center)
        {
            writer.WriteNumber("center", center);
        }

        writer.WriteEndObject();
    }

    private static void WriteJoint(Utf8JsonWriter writer, VrmSpringJoint joint)
    {
        writer.WriteStartObject();
        writer.WriteNumber("node", joint.Node);
        writer.WriteNumber("hitRadius", joint.HitRadius);
        writer.WriteNumber("stiffness", joint.Stiffness);
        writer.WriteNumber("gravityPower", joint.GravityPower);
        WriteVector(writer, "gravityDir", joint.GravityDir);
        writer.WriteNumber("dragForce", joint.DragForce);
        writer.WriteEndObject();
    }

    private static void WriteName(Utf8JsonWriter writer, string? name)
    {
        if (name is not null)
        {
            writer.WriteString("name", name);
        }
    }

    private static void WriteArray<T>(Utf8JsonWriter writer, string property, IReadOnlyList<T> values, Action<Utf8JsonWriter, T> writeElement)
    {
        if (values.Count == 0)
        {
            return;
        }

        writer.WriteStartArray(property);
        foreach (T value in values)
        {
            writeElement(writer, value);
        }

        writer.WriteEndArray();
    }

    private static void WriteVector(Utf8JsonWriter writer, string property, Vector3 value)
    {
        writer.WriteStartArray(property);
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }
}
