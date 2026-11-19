using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleAuxiliaryVertex(int Bone, NativeBattleVector Position);
public readonly record struct NativeBattleAuxiliaryTriangle(int A, int B, int C);
public readonly record struct NativeBattleAuxiliarySegment(int A, int B);

/// <summary>Immutable recipient geometry and its complete physical anatomy, independent of a donor's motion bindings.</summary>
public sealed class NativeBattleAuxiliaryContactCalibration
{
    public NativeBattleAuxiliaryContactCalibration(ImmutableArray<NativeBattleAuxiliaryVertex> vertices, ImmutableArray<NativeBattleAuxiliaryTriangle> triangles, ImmutableArray<int> physicalBones, ImmutableArray<int> contactBones, ImmutableArray<NativeBattleAuxiliarySegment> bodySegments)
    {
        if (vertices.IsDefaultOrEmpty || triangles.IsDefaultOrEmpty || physicalBones.IsDefaultOrEmpty || contactBones.IsDefaultOrEmpty || bodySegments.IsDefaultOrEmpty || vertices.Any(vertex => vertex.Bone < 0) || triangles.Any(triangle => (uint)triangle.A >= vertices.Length || (uint)triangle.B >= vertices.Length || (uint)triangle.C >= vertices.Length))
        {
            throw new InvalidDataException("Auxiliary contact requires valid recipient vertices and triangle selectors.");
        }
        if (physicalBones.Any(bone => bone < 0) || physicalBones.Distinct().Count() != physicalBones.Length || contactBones.Distinct().Count() != contactBones.Length || contactBones.Any(bone => !physicalBones.Contains(bone)) || bodySegments.Any(segment => !physicalBones.Contains(segment.A) || !physicalBones.Contains(segment.B)))
        {
            throw new InvalidDataException("Auxiliary contact requires a complete, unique physical anatomy and its admitted contact segments.");
        }
        Vertices = vertices;
        Triangles = triangles;
        PhysicalBones = physicalBones;
        ContactBones = contactBones;
        BodySegments = bodySegments;
    }

    public ImmutableArray<NativeBattleAuxiliaryVertex> Vertices { get; }
    public ImmutableArray<NativeBattleAuxiliaryTriangle> Triangles { get; }
    public ImmutableArray<int> PhysicalBones { get; }
    public ImmutableArray<int> ContactBones { get; }
    public ImmutableArray<NativeBattleAuxiliarySegment> BodySegments { get; }
}

/// <summary>Reports all observed contacts separately from the subset that auxiliary articulation can change.</summary>
public sealed record NativeBattleAuxiliaryContactReport(int ClothBodyCrossings, int ViolatingLayers, float MaximumLayerPenetration, float SharedEdgeStrain)
{
    public int ImmutableBoundaryCrossings { get; init; }

    public int CorrectableCrossings => ClothBodyCrossings - ImmutableBoundaryCrossings;
    public int HardViolatingLayers { get; init; }
    public int SoftViolatingLayers => ViolatingLayers - HardViolatingLayers;
    public float MaximumHardLayerPenetration { get; init; }
    public double HardResidualSquared { get; init; }
    public double CanonicalRestEnergy { get; init; }
    public double SoftResidualSquared { get; init; }
    public bool Converged => CorrectableCrossings == 0 && HardViolatingLayers == 0;
}
