using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect83ResourcesReader
{
    public static NativeBattleEffect83Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag082.tim\0")
        {
            throw new InvalidDataException("Effect83 requires its original initializer texture name.");
        }
        ImmutableArray<byte> bytes = memory.Read(ModelAddress, ModelLength).ToImmutableArray();
        int[] sections = Enumerable.Range(0, 5).Select(index => NativeBattleDatDirectory.Section(bytes.AsSpan(), index)).ToArray();
        if (!sections.SequenceEqual(new[] { 24, 1336, 6152, 10252, 10252 }))
        {
            throw new InvalidDataException("Effect83 requires its complete original four-section effect model.");
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        _ = NativeBattlePoseDecoder.DecodeFrames(pose.Animations.AsSpan(), pose.BoneCount);
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        NativeBattleEffectOwnedModelResource model = new(ModelAddress, bytes, pose, geometry);
        NativeBattleEffect83NodeResourceBank nodes = new(memory.Read(NodeSourceAddress, NodeSourceLength).ToImmutableArray());
        nodes.Validate();
        for (int index = 0; index < 16; index++)
        {
            uint sprite = nodes.Directory(1, index);
            if (sprite != 0)
            {
                ImmutableArray<byte> resource = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, NodeSourceAddress + sprite);
                RequireBounded(sprite, resource.Length);
            }
            uint mesh = nodes.Directory(2, index);
            if (mesh != 0)
            {
                NativeBattleStaticMeshResource resource = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, NodeSourceAddress + mesh);
                RequireBounded(mesh, resource.Bytes.Length);
            }
            uint morph = nodes.Directory(3, index);
            if (morph != 0)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(NodeSourceAddress + morph + 4, 4));
                int length = checked(8 + count * 8);
                if (count != 90 || BinaryPrimitives.ReadInt32LittleEndian(memory.Read(NodeSourceAddress + morph, 4)) != length)
                {
                    throw new InvalidDataException("Effect83 requires both original ninety-vertex morph fronts.");
                }
                RequireBounded(morph, length);
            }
        }
        NativeBattleEffect83Resources result = new(mainTim, new("FF8_EN:1738C08", memory.Read(EmbeddedTimAddress, EmbeddedTimLength).ToImmutableArray()), new("FF8_EN:1578444", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(TargetPointerAddress, 4)), model, nodes);
        result.Validate();
        return result;

        static void RequireBounded(uint offset, int length)
        {
            if (length < 0 || (ulong)offset + (uint)length > NodeSourceLength)
            {
                throw new InvalidDataException("Effect83 node payload leaves its exact original bank.");
            }
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect83Resources? nativeEffect83Resources;

    public NativeBattleEffect83Resources ReadNativeEffect83Resources()
    {
        if (nativeEffect83Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectInitializerAsset mainTim = new("mag082.tim", Read(magic, "mag082.tim").ToImmutableArray());
            nativeEffect83Resources = NativeBattleEffect83ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), mainTim);
        }
        return nativeEffect83Resources;
    }
}
