using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed record Ff7AppearanceCalibration(Matrix4x4 Transform, float FacingRadians, Vector3 SourceFootDirection, Vector3 TargetFootDirection, bool FirstHandIsLeft, float HandSideAlignment);

internal sealed partial class Ff7ModelAppearance
{
    public const string StandardFieldSquallProfileVersion = "ff7-cloud-aaaa-to-ff8-field-d000-v1";

    private const string StandardFieldSquallRig = "ff8/field/rig/EC6C2019C6DCE78C9139054606A84422C426A40840EDE4D2B0EBA63E16893FE1";
    private const string OpeningCloudRig = "0F96ADB43ECEDBBC0375FBA00AE29DB330F725A049F2F04915FAA339D71E3EED";

    public string ProfileId { get; private init; } = "explicit-field-appearance";

    /// <summary>Creates the explicit opening Cloud-to-standard-field-Squall profile from caller-selected installations.</summary>
    public static Ff7ModelAppearance CreateStandardFieldSquall(Ff7Installation installation, GameAssets assets)
    {
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(characters, field.Models.Models[0]);
        ModelCatalogEntry entry = assets.GetFieldModelSource("bghall_1", 0);
        var catalog = Memoria.FF8.Assets.FieldCharacterCatalog.Create([entry], assets.LoadFieldModelSource);
        FieldModel target = assets.LoadFieldModelSource(entry);
        if (entry.ModelLibraryResource != "field/main_chr/d000.mch" || catalog.GetSource(entry.Id).RigId != StandardFieldSquallRig || Ff7SemanticRetargeter.HierarchyIdentity(original.Hierarchy) != OpeningCloudRig)
        {
            throw new NotSupportedException("The selected field source does not implement the standard field Squall rig profile.");
        }
        Ff7AppearanceCalibration measured = CalibrateNeutralGeometry(target, field.Models.Models[0], original, [19, 20], 21, 22);
        if (!measured.FirstHandIsLeft)
        {
            throw new NotSupportedException("The selected standard field geometry does not retain the calibrated anatomical sides.");
        }
        Ff7SemanticRetargeter retargeter = new(target, field.Models.Models[0], original, measured.Transform, StandardFieldSquallJoints, StandardFieldSquallFeet);
        Dictionary<string, string> motions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["acfe.a"] = catalog.GetAnimation(entry.Id, 0).Id,
            ["aaff.a"] = catalog.GetAnimation(entry.Id, 1).Id,
            ["aaga.a"] = catalog.GetAnimation(entry.Id, 2).Id
        };
        return new(assets, catalog, entry.Id, "aaaa.hrc", measured.Transform, motions, retargeter, ["aaff.a", "aaga.a"], ["bvjf.a"])
        {
            ProfileId = StandardFieldSquallProfileVersion + "/" + catalog.GetSource(entry.Id).RigId
        };
    }

    internal static Ff7FootContactBinding StandardFieldSquallFeet { get; } = new(["l_foot", "r_foot"], [19, 20]);

    internal static IReadOnlyList<Ff7SemanticJointBinding> StandardFieldSquallJoints { get; } = Array.AsReadOnly<Ff7SemanticJointBinding>(
    [
        new("hip", 2), new("chest", 1), new("head", 9),
        new("l_chest", 3), new("l_collar", 8), new("l_uparm", 13), new("l_foarm", 17), new("l_hand", 21),
        new("r_chest", 5), new("r_collar", 10), new("r_uparm", 14), new("r_foarm", 18), new("r_hand", 22),
        new("l_hip", 6), new("l_femur", 11), new("l_tibia", 15), new("l_foot", 19),
        new("r_hip", 7), new("r_femur", 12), new("r_tibia", 16), new("r_foot", 20)
    ]);
    /// <summary>An explicit geometry policy: align the average ankle-to-foot-mesh direction, then match neutral height/foot anchor.</summary>
    public static Ff7AppearanceCalibration CalibrateNeutralGeometry(FieldModel target, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, IReadOnlyList<int> targetFeet, int firstHand, int secondHand)
    {
        if (targetFeet.Count != 2 || targetFeet.Any(bone => (uint)bone >= target.Bones.Count) || (uint)firstHand >= target.Bones.Count || (uint)secondHand >= target.Bones.Count)
        {
            throw new InvalidDataException("Geometry calibration requires explicit valid target foot/hand attachment owners.");
        }
        string idle = Path.GetFileNameWithoutExtension(descriptor.Animations[0]) + ".a";
        IReadOnlyList<Matrix4x4> sourcePose = Ff7FieldRenderer.Pose(original, original.Animations[idle], 0);
        Matrix4x4 sourceBasis = Ff7FieldRenderer.ModelBasis(descriptor);
        Vector3 SourceCentre(string name)
        {
            int bone = original.Hierarchy.Bones.Single(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Index;
            Vector3[] vertices = original.Geometry.Where(part => part.BoneIndex == bone).SelectMany(part => part.Polygon.Vertices.Select(vertex => Vector3.Transform(vertex, sourcePose[bone] * sourceBasis))).ToArray();
            return Centre(vertices);
        }
        Vector3 SourcePivot(string name)
        {
            int bone = original.Hierarchy.Bones.Single(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Index;
            return (sourcePose[bone] * sourceBasis).Translation;
        }
        Vector3[] targetPose = target.Pose(0, 0);
        IReadOnlyList<Matrix4x4> targetBones = target.ReadBoneTransforms(0, 0);
        Vector3 TargetCentre(int bone) => Centre(target.Vertices.Select((vertex, index) => (vertex.Bone, Position: targetPose[index])).Where(item => item.Bone == bone).Select(item => item.Position).ToArray());
        Vector3 sourceForward = Horizontal((SourceCentre("l_foot") - SourcePivot("l_foot") + SourceCentre("r_foot") - SourcePivot("r_foot")) / 2);
        Vector3 targetForward = Horizontal(targetFeet.Select(bone => TargetCentre(bone) - targetBones[bone].Translation).Aggregate(Vector3.Zero, (sum, vector) => sum + vector) / 2);
        float yaw = MathF.Atan2(sourceForward.Y, sourceForward.X) - MathF.Atan2(targetForward.Y, targetForward.X);
        Matrix4x4 basis = Matrix4x4.CreateRotationZ(yaw);
        Matrix4x4 calibration = MatchOriginalNeutralBounds(target, descriptor, original, basis);
        Vector3 sourceSide = Horizontal(SourceCentre("l_hand") - SourceCentre("r_hand"));
        Vector3 targetSide = Horizontal(Vector3.TransformNormal(TargetCentre(firstHand) - TargetCentre(secondHand), basis));
        float alignment = Vector3.Dot(sourceSide, targetSide);
        return new(calibration, yaw, sourceForward, targetForward, alignment > 0, alignment);
    }

    private static Vector3 Centre(Vector3[] vertices)
    {
        if (vertices.Length == 0)
        {
            throw new InvalidDataException("A named calibration attachment has no original mesh vertices.");
        }
        return vertices.Aggregate(Vector3.Zero, (sum, vector) => sum + vector) / vertices.Length;
    }

    private static Vector3 Horizontal(Vector3 vector)
    {
        vector.Z = 0;
        if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y) || vector.LengthSquared() == 0)
        {
            throw new InvalidDataException("Neutral geometry does not establish a finite horizontal anatomical direction.");
        }
        return Vector3.Normalize(vector);
    }
}
