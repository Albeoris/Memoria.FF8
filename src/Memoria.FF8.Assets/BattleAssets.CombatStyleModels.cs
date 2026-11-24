using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private readonly Dictionary<(int Recipient, int Costume, int Source, BattleModel RecipientModel, BattleModel SourceModel, BattleModel? ContactWeapon), BattleModel> combatStyleModels = [];

    public BattleModel LoadPlayerForWeapon(int character, int costume, int? weaponId)
    {
        return LoadPlayerForStyle(character, costume, weaponId is int weapon ? WeaponOwner(weapon) : character, weaponId);
    }

    public BattleModel LoadPlayerForStyle(int character, int costume, int sourceCharacter, int? sourceWeaponId = null)
    {
        return WithPlayerModelResources(() => ReadPlayerForStyle(character, costume, sourceCharacter, sourceWeaponId));
    }

    private BattleModel ReadPlayerForStyle(int character, int costume, int sourceCharacter, int? sourceWeaponId)
    {
        BattleModel recipient = LoadPlayer(character, costume);
        if (sourceCharacter == character)
        {
            return recipient;
        }
        BattleModel source = LoadPlayer(sourceCharacter);
        _ = BattleHumanoidRigProfiles.SemanticPairs(sourceCharacter, source, character, recipient);
        BattleModel? contactWeapon = ReadContactWeapon(sourceCharacter, source, sourceWeaponId);
        if (combatStyleModels.TryGetValue((character, costume, sourceCharacter, recipient, source, contactWeapon), out BattleModel? cached))
        {
            return cached;
        }
        foreach (var obsolete in combatStyleModels.Keys.Where(key => key.Recipient == character && key.Costume == costume && key.Source == sourceCharacter).ToArray())
        {
            combatStyleModels.Remove(obsolete);
        }
        NativeBattlePoseSource sourceMotion = source.NativePoseSource!;
        var contacts = contactWeapon is null ? default : BattleWeaponContactFrames.Read(sourceCharacter, source, contactWeapon, sourceMotion);
        NativeBattlePoseSource motion = BattleHumanoidRigProfiles.CreateRetargeter(sourceCharacter, source, character, recipient).Compile(sourceMotion, contacts);
        IReadOnlyList<BattleAnimation> animations = NativeBattlePoseDecoder.DecodeFrames(motion.Animations.AsSpan(), motion.BoneCount).Select(frames => new BattleAnimation(frames.Select(pose => new BattlePose(new Vector3(pose.Translation.X, pose.Translation.Y, pose.Translation.Z) / 100f, pose.Bones.Select(bone => new Vector3(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z) * (MathF.Tau / 4096)).ToArray(), pose.Bones.Select(bone => new Vector3(bone.Scale.X, bone.Scale.Y, bone.Scale.Z) / 1024f).ToArray())).ToArray())).ToArray();
        IReadOnlyList<BattleMesh> meshes = recipient.Meshes;
        ImmutableArray<byte>? geometry = recipient.NativeGeometryBytes;
        if (sourceCharacter is 1 or 9 || character is 1 or 9)
        {
            BattleMesh empty = new([], []) { NativeBoneGroups = [], NativePackedVertexGroups = [] };
            meshes = [recipient.Meshes[0], empty];
            if (recipient.NativeGeometryBytes is { } selectedGeometry)
            {
                geometry = ReplaceEquipmentBodyObject(selectedGeometry.AsSpan(), recipient.Meshes[0].Vertices.Count);
            }
            else
            {
                NativeBattleModelDatSource original = ReadNativeModelDatSource(checked((byte)character), false, checked((byte)costume));
                int start = original.Section(1);
                geometry = ReplaceEquipmentBodyObject(original.Bytes.AsSpan().Slice(start, original.Section(2) - start), recipient.Meshes[0].Vertices.Count);
            }
        }
        if (character == 4)
        {
            meshes = meshes.Select((mesh, index) => index == 0 ? SelectRinoaLauncherMount(mesh, recipient, false) : mesh).ToArray();
            geometry = EncodeNativeBodyGeometry(meshes);
            meshes = RefreshNativeVertexGroups(meshes, geometry.Value);
        }
        if (sourceCharacter == 1 && character != 1)
        {
            int[] hands = BattleHumanoidRigProfiles.SemanticPairs(character, recipient, character, recipient).Where(pair => pair.Joint is "hand-a" or "hand-b").Select(pair => pair.Recipient).ToArray();
            meshes = meshes.Select(mesh => MaskBodyHands(mesh, hands)).ToArray();
            geometry = EncodeNativeBodyGeometry(meshes);
            meshes = RefreshNativeVertexGroups(meshes, geometry.Value);
        }
        BattleModel result = recipient with { NativePoseSource = motion, Animations = animations, NativeActionCameraBank = source.NativeActionCameraBank, Sequences = source.Sequences, Meshes = meshes, NativeControlMeshes = meshes, NativeRenderGeometry = NativeBattleGeometryMetadata.FromMeshes(meshes), NativeGeometryBytes = geometry };
        combatStyleModels.Add((character, costume, sourceCharacter, recipient, source, contactWeapon), result);
        return result;
    }

    private BattleModel? ReadContactWeapon(int sourceCharacter, BattleModel sourceBody, int? preferredWeapon)
    {
        if (sourceCharacter is not (2 or 8))
        {
            return null;
        }
        int selected = preferredWeapon is int weapon && WeaponOwner(weapon) == sourceCharacter ? weapon : WeaponDefinitions.First(weapon => weapon.CharacterId == sourceCharacter).Id;
        return ResolvePlayerRenderWeapon(sourceCharacter, selected, sourceBody) ?? throw new InvalidDataException("A firearm source contact requires its actual independent equipment.");
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
        if (weapon.EditableOverride is not null)
        {
            throw new NotSupportedException("The shared equipment override requires an explicit semantic attachment profile.");
        }
        Dictionary<int, NativeBattleSemanticBonePair> joints = BattleHumanoidRigProfiles.SemanticPairs(sourceCharacter, sourceBody, recipientCharacter, recipient).ToDictionary(pair => pair.Source);
        float sourceScale = sourceBody.NativePoseSource!.ReadModelScale5027D0();
        float recipientScale = recipient.NativePoseSource!.ReadModelScale5027D0();
        if (sourceScale <= 0 || recipientScale <= 0)
        {
            throw new NotSupportedException("Shared equipment unit conversion requires positive source and recipient skeleton scales.");
        }
        float units = sourceScale / recipientScale;
        NativeBattleModelDatSource original = ReadNativeModelDatSource(checked((byte)sourceCharacter), true, checked((byte)WeaponDefinitions[weaponId].Tier));
        int start = original.Section(0);
        byte[] bytes = weapon.NativeGeometryBytes is { } selectedGeometry ? selectedGeometry.ToArray() : original.Bytes.AsSpan().Slice(start, original.Section(1) - start).ToArray();
        if (sourceCharacter == 9 && recipientCharacter != 9)
        {
            bytes = EncodeNativeBodyGeometry(weapon.Meshes.Select(mesh => SelectKirosHandComponents(mesh, false)).ToArray()).ToArray();
        }
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
                NativeBattleSemanticBonePair joint = joints.TryGetValue(sourceBone, out NativeBattleSemanticBonePair? pair) ? pair : throw new NotSupportedException("The shared weapon reaches a bone outside the admitted semantic equipment joints.");
                Matrix4x4 sourceToRecipient = joint.SourceBindAxes * Matrix4x4.Transpose(joint.RecipientBindAxes);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor), checked((ushort)joint.Recipient));
                for (int vertex = 0; vertex < vertices; vertex++)
                {
                    int offset = cursor + 4 + vertex * 6;
                    Vector3 originalPosition = new(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 4)));
                    Vector3 position = Vector3.TransformNormal(originalPosition, sourceToRecipient) * units;
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), checked((short)MathF.Round(position.X)));
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), checked((short)MathF.Round(position.Y)));
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), checked((short)MathF.Round(position.Z)));
                }
                cursor = checked(cursor + 4 + vertices * 6);
            }
        }
        Dictionary<int, ImmutableArray<byte>> selectedSections = new() { [0] = bytes.ToImmutableArray() };
        if (weapon.NativeWeaponTexturePack is { } selectedTextures)
        {
            selectedSections[4] = selectedTextures.Data;
        }
        NativeBattleModelDatSource rewritten = ReplaceNativeDatSections(original, selectedSections);
        BattleModel rebound = BattleResourceReader.ReadWeapon(weapon.Name, rewritten.Bytes.ToArray(), recipient);
        return rebound with { Textures = weapon.Textures, TextureRegions = weapon.TextureRegions, TextureSourceAddresses = weapon.TextureSourceAddresses, TextureSourceDescriptor = weapon.TextureSourceDescriptor, Sequences = weapon.Sequences, NativeRenderGeometry = NativeBattleGeometryMetadata.FromMeshes(rebound.Meshes), NativeGeometryBytes = bytes.ToImmutableArray() };
    }

    private static ImmutableArray<byte> ReplaceEquipmentBodyObject(ReadOnlySpan<byte> original, int maximumVertices)
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
        writer.Write(maximumVertices);
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
