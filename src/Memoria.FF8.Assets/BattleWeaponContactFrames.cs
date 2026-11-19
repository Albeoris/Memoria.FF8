using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class BattleWeaponContactFrames
{
    private static readonly ConditionalWeakTable<BattleModel, ConcurrentDictionary<(int Character, BattleModel Weapon, string BodyRevision, string WeaponRevision), ImmutableArray<ImmutableArray<bool>>>> cache = new();

    public static ImmutableArray<ImmutableArray<bool>> Read(int sourceCharacter, BattleModel sourceBody, BattleModel sourceWeapon, NativeBattlePoseSource bodyMotion, NativeBattlePoseSource? weaponMotion = null)
    {
        if (sourceCharacter is not (2 or 8))
        {
            return default;
        }
        NativeBattlePoseSource linked = weaponMotion ?? sourceWeapon.NativePoseSource ?? throw new InvalidDataException("A firearm contact profile requires its source linked motion.");
        if (!bodyMotion.Skeleton.AsSpan().SequenceEqual(sourceBody.NativePoseSource!.Skeleton.AsSpan()) || !linked.Skeleton.AsSpan().SequenceEqual(sourceWeapon.NativePoseSource!.Skeleton.AsSpan()))
        {
            throw new InvalidDataException("Source contact motion no longer matches its calibrated body and equipment rigs.");
        }
        string bodyRevision = Convert.ToHexString(SHA256.HashData(bodyMotion.Animations.AsSpan()));
        string weaponRevision = Convert.ToHexString(SHA256.HashData(linked.Animations.AsSpan()));
        return cache.GetOrCreateValue(sourceBody).GetOrAdd((sourceCharacter, sourceWeapon, bodyRevision, weaponRevision), _ => Build(sourceCharacter, sourceBody, sourceWeapon, bodyMotion, linked));
    }

    private static ImmutableArray<ImmutableArray<bool>> Build(int sourceCharacter, BattleModel body, BattleModel weapon, NativeBattlePoseSource bodyMotion, NativeBattlePoseSource weaponMotion)
    {
        if (!bodyMotion.Skeleton.AsSpan().SequenceEqual(body.NativePoseSource!.Skeleton.AsSpan()) || !weaponMotion.Skeleton.AsSpan().SequenceEqual(weapon.NativePoseSource!.Skeleton.AsSpan()))
        {
            throw new InvalidDataException("Source weapon contact requires the actual body and weapon skeletons.");
        }
        int hand = BattleHumanoidRigProfiles.SemanticPairs(sourceCharacter, body, sourceCharacter, body).Single(pair => pair.Joint == "hand-a").Source;
        Vector3[] handVertices = body.Meshes.SelectMany(mesh => mesh.Vertices).Where(vertex => vertex.Bone == hand).Select(vertex => Point(vertex.NativePosition ?? throw new InvalidDataException("The authored hand envelope requires native local vertices."))).ToArray();
        if (handVertices.Length == 0)
        {
            throw new InvalidDataException("The source secondary hand has no authored rigid surface.");
        }
        Vector3 minimum = handVertices.Aggregate(Vector3.Min);
        Vector3 maximum = handVertices.Aggregate(Vector3.Max);
        var bodyFrames = NativeBattlePoseDecoder.DecodeFrames(bodyMotion.Animations.AsSpan(), bodyMotion.BoneCount);
        var weaponFrames = NativeBattlePoseDecoder.DecodeFrames(weaponMotion.Animations.AsSpan(), weaponMotion.BoneCount);
        ImmutableArray<ImmutableArray<bool>>.Builder contacts = ImmutableArray.CreateBuilder<ImmutableArray<bool>>(bodyFrames.Length);
        for (int animation = 0; animation < bodyFrames.Length; animation++)
        {
            ImmutableArray<bool>.Builder admitted = ImmutableArray.CreateBuilder<bool>(bodyFrames[animation].Length);
            NativeBattlePoseProgress? progress = animation < weaponFrames.Length ? new(checked((byte)animation), 1, checked((byte)weaponFrames[animation].Length), 0) : null;
            foreach (NativeBattleDecodedPose bodyPose in bodyFrames[animation])
            {
                bool touching = false;
                if (progress is not null)
                {
                    NativeBattleActionMatrix handMatrix = NativeBattlePoseMatrices.StageBoneMatrices(bodyMotion, bodyPose)[hand];
                    if (!Matrix4x4.Invert(Matrix(handMatrix), out Matrix4x4 inverseHand))
                    {
                        throw new InvalidDataException("A source hand envelope requires an invertible posed hand matrix.");
                    }
                    var weaponMatrices = NativeBattlePoseMatrices.StageBoneMatrices(weaponMotion, weaponFrames[animation][progress.Frame - 1]);
                    foreach (BattleMesh mesh in weapon.Meshes)
                    {
                        Vector3[] vertices = mesh.Vertices.Select(vertex =>
                        {
                            NativeBattleActionMatrix bone = weaponMatrices[vertex.Bone];
                            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(bone, vertex.NativePosition ?? throw new InvalidDataException("Source weapon contact requires native local vertices."));
                            return Vector3.Transform(new Vector3(rotated.X + bone.X, rotated.Y + bone.Y, rotated.Z + bone.Z), inverseHand);
                        }).ToArray();
                        touching = mesh.Faces.Any(face => face.Vertices.Length == 3
                            ? NativeBattleWeaponContactGeometry.IntersectsHandEnvelope(minimum, maximum, vertices[face.Vertices[0]], vertices[face.Vertices[1]], vertices[face.Vertices[2]])
                            : NativeBattleWeaponContactGeometry.IntersectsHandEnvelope(minimum, maximum, vertices[face.Vertices[0]], vertices[face.Vertices[1]], vertices[face.Vertices[3]]) || NativeBattleWeaponContactGeometry.IntersectsHandEnvelope(minimum, maximum, vertices[face.Vertices[3]], vertices[face.Vertices[2]], vertices[face.Vertices[0]]));
                        if (touching)
                        {
                            break;
                        }
                    }
                    progress = progress.Advance();
                }
                admitted.Add(touching);
            }
            contacts.Add(admitted.MoveToImmutable());
        }
        return contacts.MoveToImmutable();
    }

    private static Vector3 Point(NativeBattleVector point) => new(point.X, point.Y, point.Z);

    private static Matrix4x4 Matrix(NativeBattleActionMatrix matrix)
    {
        var r = matrix.Rotation;
        return new(r[0] / 4096f, r[3] / 4096f, r[6] / 4096f, 0, r[1] / 4096f, r[4] / 4096f, r[7] / 4096f, 0, r[2] / 4096f, r[5] / 4096f, r[8] / 4096f, 0, matrix.X, matrix.Y, matrix.Z, 1);
    }
}
