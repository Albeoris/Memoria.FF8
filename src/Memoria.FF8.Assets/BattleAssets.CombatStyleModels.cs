using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private readonly Dictionary<(int Recipient, int Costume, int Source, BattleModel RecipientModel, BattleModel SourceModel), BattleModel> combatStyleModels = [];

    public BattleModel LoadPlayerForWeapon(int character, int costume, int? weaponId)
    {
        return LoadPlayerForStyle(character, costume, weaponId is int weapon ? WeaponOwner(weapon) : character);
    }

    public BattleModel LoadPlayerForStyle(int character, int costume, int sourceCharacter)
    {
        BattleModel recipient = LoadPlayer(character, costume);
        if (sourceCharacter == character)
        {
            return recipient;
        }
        BattleModel source = LoadPlayer(sourceCharacter);
        _ = BattleHumanoidRigProfiles.SemanticPairs(sourceCharacter, source, character, recipient);
        if (combatStyleModels.TryGetValue((character, costume, sourceCharacter, recipient, source), out BattleModel? cached))
        {
            return cached;
        }
        foreach (var obsolete in combatStyleModels.Keys.Where(key => key.Recipient == character && key.Costume == costume && key.Source == sourceCharacter).ToArray())
        {
            combatStyleModels.Remove(obsolete);
        }
        NativeBattlePoseSource motion = BattleHumanoidRigProfiles.CreateRetargeter(sourceCharacter, source, character, recipient).Compile(source.NativePoseSource!);
        IReadOnlyList<BattleAnimation> animations = NativeBattlePoseDecoder.DecodeFrames(motion.Animations.AsSpan(), motion.BoneCount).Select(frames => new BattleAnimation(frames.Select(pose => new BattlePose(new Vector3(pose.Translation.X, pose.Translation.Y, pose.Translation.Z) / 100f, pose.Bones.Select(bone => new Vector3(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z) * (MathF.Tau / 4096)).ToArray(), pose.Bones.Select(bone => new Vector3(bone.Scale.X, bone.Scale.Y, bone.Scale.Z) / 1024f).ToArray())).ToArray())).ToArray();
        IReadOnlyList<BattleMesh> meshes = recipient.Meshes;
        ImmutableArray<byte>? geometry = recipient.NativeGeometryBytes;
        if (sourceCharacter is 1 or 9 || character is 1 or 9)
        {
            BattleMesh empty = new([], []) { NativeBoneGroups = [], NativePackedVertexGroups = [] };
            meshes = [recipient.Meshes[0], empty];
            NativeBattleModelDatSource original = ReadNativeModelDatSource(checked((byte)character), false, checked((byte)costume));
            int start = original.Section(1);
            geometry = ReplaceEquipmentBodyObject(original.Bytes.AsSpan().Slice(start, original.Section(2) - start));
        }
        BattleModel result = recipient with { NativePoseSource = motion, Animations = animations, NativeActionCameraBank = source.NativeActionCameraBank, Sequences = source.Sequences, Meshes = meshes, NativeControlMeshes = meshes, NativeRenderGeometry = NativeBattleGeometryMetadata.FromMeshes(meshes), NativeGeometryBytes = geometry };
        combatStyleModels.Add((character, costume, sourceCharacter, recipient, source), result);
        return result;
    }

    private int WeaponOwner(int weapon)
    {
        if ((uint)weapon >= WeaponDefinitions.Count)
        {
            throw new InvalidDataException("The selected battle weapon has no installed definition.");
        }
        return WeaponDefinitions[weapon].CharacterId;
    }

    private BattleModel RebindSharedWeapon(int weaponId, int sourceCharacter, BattleModel sourceBody, int recipientCharacter, BattleModel recipient, BattleModel weapon)
    {
        Dictionary<int, int> joints = BattleHumanoidRigProfiles.SemanticPairs(sourceCharacter, sourceBody, recipientCharacter, recipient).ToDictionary(pair => pair.Source, pair => pair.Recipient);
        int Bone(int sourceBone) => joints.TryGetValue(sourceBone, out int targetBone) ? targetBone : throw new NotSupportedException("The shared weapon reaches a bone outside the admitted semantic equipment joints.");
        BattleMesh[] meshes = weapon.Meshes.Select(mesh => mesh with
        {
            Vertices = mesh.Vertices.Select(vertex => vertex with { Bone = Bone(vertex.Bone) }).ToArray(),
            NativeBoneGroups = mesh.NativeBoneGroups?.Select(bone => checked((short)Bone(bone))).ToImmutableArray(),
            NativePackedVertexGroups = mesh.NativePackedVertexGroups?.Select(group => group with { Bone = checked((ushort)Bone(group.Bone)) }).ToImmutableArray()
        }).ToArray();
        NativeBattleModelDatSource original = ReadNativeModelDatSource(checked((byte)sourceCharacter), true, checked((byte)WeaponDefinitions[weaponId].Tier));
        int start = original.Section(0);
        byte[] bytes = original.Bytes.AsSpan().Slice(start, original.Section(1) - start).ToArray();
        int objects = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        for (int index = 0; index < objects; index++)
        {
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4 + index * 4));
            int groups = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor));
            cursor += 2;
            for (int group = 0; group < groups; group++)
            {
                int sourceBone = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor));
                int vertices = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor + 2));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor), checked((ushort)Bone(sourceBone)));
                cursor = checked(cursor + 4 + vertices * 6);
            }
        }
        return weapon with { Bones = recipient.Bones, Animations = recipient.Animations, NativePoseSource = recipient.NativePoseSource, Scale = recipient.Scale, Meshes = meshes, NativeControlMeshes = meshes, NativeRenderGeometry = NativeBattleGeometryMetadata.FromMeshes(meshes), NativeGeometryBytes = bytes.ToImmutableArray() };
    }

    private static ImmutableArray<byte> ReplaceEquipmentBodyObject(ReadOnlySpan<byte> original)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(original);
        if (count is < 1 or > 2)
        {
            throw new NotSupportedException("The standard battle body requires one body object and at most one embedded equipment object.");
        }
        int start = BinaryPrimitives.ReadInt32LittleEndian(original[4..]);
        int end = count == 2 ? BinaryPrimitives.ReadInt32LittleEndian(original[8..]) : original.Length;
        using MemoryStream output = new();
        using BinaryWriter writer = new(output);
        writer.Write(2);
        writer.Write(12);
        writer.Write(12 + end - start);
        writer.Write(original[start..end]);
        writer.Write(0);
        writer.Write(0);
        return output.ToArray().ToImmutableArray();
    }

    private static NativeBattleModelDatSource ReplaceNativeDatSections(NativeBattleModelDatSource source, IReadOnlyDictionary<int, ImmutableArray<byte>> replacements)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan());
        using MemoryStream output = new();
        using BinaryWriter writer = new(output);
        writer.Write(count);
        for (int section = 0; section <= count; section++)
        {
            writer.Write(0);
        }
        for (int section = 0; section < count; section++)
        {
            while ((output.Length & 3) != 0)
            {
                output.Position = output.Length;
                writer.Write((byte)0);
            }
            int pointer = checked((int)output.Length);
            output.Position = 4 + section * 4;
            writer.Write(pointer);
            output.Position = pointer;
            ImmutableArray<byte> bytes = replacements.TryGetValue(section, out ImmutableArray<byte> replacement) ? replacement : source.Bytes.Slice(source.Section(section), source.Section(section + 1) - source.Section(section));
            writer.Write(bytes.AsSpan());
        }
        output.Position = 4 + count * 4;
        writer.Write(checked((int)output.Length));
        return source with { Bytes = output.ToArray().ToImmutableArray() };
    }
}
