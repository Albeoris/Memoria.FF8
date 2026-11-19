using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class BattleAuxiliaryContactCalibrations
{
    private sealed record Entry(NativeBattleAuxiliaryContactCalibration? Calibration);
    private static readonly ConditionalWeakTable<BattleModel, Entry> Entries = new();

    public static NativeBattleAuxiliaryContactCalibration? Create(int character, BattleModel recipient)
    {
        IReadOnlyList<NativeBattleSemanticBonePair> physical = BattleHumanoidRigProfiles.SemanticPairs(character, recipient, character, recipient);
        return Entries.GetValue(recipient, model => new(Build(model, physical))).Calibration;
    }

    private static NativeBattleAuxiliaryContactCalibration? Build(BattleModel recipient, IReadOnlyList<NativeBattleSemanticBonePair> physical)
    {
        HashSet<int> physicalBones = physical.Select(pair => pair.Recipient).ToHashSet();
        HashSet<int> ancestors = [];
        foreach (int bone in physicalBones)
        {
            for (int current = bone; current >= 0; current = recipient.Bones[current].Parent)
            {
                ancestors.Add(current);
            }
        }
        HashSet<int> auxiliary = Enumerable.Range(0, recipient.Bones.Count).Where(bone => !ancestors.Contains(bone)).ToHashSet();
        if (auxiliary.Count == 0)
        {
            return null;
        }
        NativeBattleRawMesh mesh = recipient.NativeRenderGeometry is { IsDefaultOrEmpty: false } geometry ? geometry[0] : NativeBattleGeometryMetadata.FromMeshes([recipient.Meshes[0]])[0];
        mesh.Validate();
        ImmutableArray<NativeBattleAuxiliaryVertex> vertices = mesh.Vertices.Select(vertex => new NativeBattleAuxiliaryVertex(vertex.Bone, vertex.Position)).ToImmutableArray();
        ImmutableArray<NativeBattleAuxiliaryTriangle>.Builder triangles = ImmutableArray.CreateBuilder<NativeBattleAuxiliaryTriangle>();
        foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
        {
            int count = polygon.Quad ? 4 : 3;
            if (polygon.Bytes.Length < count * 2)
            {
                throw new InvalidDataException("Auxiliary contact calibration requires complete native polygon selectors.");
            }
            int[] indices = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(index * 2)) & 0xFFF).ToArray();
            if (indices.Any(index => index >= vertices.Length))
            {
                throw new InvalidDataException("Auxiliary contact polygon selectors exceed the selected recipient geometry.");
            }
            triangles.Add(new(indices[0], indices[1], indices[2]));
            if (polygon.Quad)
            {
                triangles.Add(new(indices[1], indices[3], indices[2]));
            }
        }
        int Joint(string name) => physical.Single(pair => pair.Joint == name).Recipient;
        ImmutableArray<int> contactBones = physical.Where(pair => pair.Joint is "hip-a" or "thigh-a" or "shin-a" or "foot-a" or "hip-b" or "thigh-b" or "shin-b" or "foot-b").Select(pair => pair.Recipient).ToImmutableArray();
        bool HasAuxiliary(NativeBattleAuxiliaryTriangle triangle) => auxiliary.Contains(vertices[triangle.A].Bone) || auxiliary.Contains(vertices[triangle.B].Bone) || auxiliary.Contains(vertices[triangle.C].Bone);
        bool IsBody(NativeBattleAuxiliaryTriangle triangle) => new[] { triangle.A, triangle.B, triangle.C }.All(index => physicalBones.Contains(vertices[index].Bone)) && new[] { triangle.A, triangle.B, triangle.C }.Any(index => contactBones.Contains(vertices[index].Bone));
        if (!triangles.Any(HasAuxiliary) || !triangles.Any(IsBody))
        {
            return null;
        }
        ImmutableArray<NativeBattleAuxiliarySegment>.Builder segments = ImmutableArray.CreateBuilder<NativeBattleAuxiliarySegment>();
        foreach (string side in new[] { "a", "b" })
        {
            int[] chain = [Joint("pelvis"), Joint($"hip-{side}"), Joint($"thigh-{side}"), Joint($"shin-{side}"), Joint($"foot-{side}")];
            for (int index = 1; index < chain.Length; index++)
            {
                segments.Add(new(chain[index - 1], chain[index]));
            }
        }
        return new(vertices, triangles.ToImmutable(), physicalBones.Order().ToImmutableArray(), contactBones, segments.ToImmutable());
    }
}
