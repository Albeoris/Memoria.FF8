using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect140Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect140ResourcesReader
{
    public static NativeBattleEffect140Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, ImmutableDictionary<int, NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag139.tim\0")
        {
            throw new InvalidDataException("Effect140 initializer texture name changed.");
        }
        ImmutableArray<byte> bytes = Bytes(ModelAddress, ModelLength);
        int[] sections = Enumerable.Range(0, 5).Select(index => NativeBattleDatDirectory.Section(bytes.AsSpan(), index)).ToArray();
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        _ = NativeBattlePoseDecoder.DecodeFrames(pose.Animations.AsSpan(), pose.BoneCount);
        NativeBattleEffectOwnedModelResource model = new(ModelAddress, bytes, pose, NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount));
        ImmutableDictionary<uint, NativeBattleGfAnimationSource> programmes = ProgrammeAddresses.Select((address, index) => new NativeBattleGfAnimationSource(address, Bytes(address, ProgrammeLengths[index]))).ToImmutableDictionary(source => source.Address);
        ImmutableDictionary<uint, NativeBattleStaticMeshResource> meshes = MeshAddresses.Select((address, index) => new NativeBattleStaticMeshResource(0, address, Bytes(address, MeshLengths[index]))).ToImmutableDictionary(source => source.Address);
        NativeBattleModuleResource sprite = new(0, SpriteAddress, 0, 0, Bytes(SpriteAddress, SpriteLength));
        NativeBattleModuleResource earlySprite = new(0, 0x11D0164, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x11D0164));
        Dictionary<uint, ImmutableArray<byte>> fronts = new() { [ModelAddress] = bytes, [VibrationAddress] = Bytes(VibrationAddress, VibrationLength), [ColorBlendAddress] = Bytes(ColorBlendAddress, 4), [SpriteAddress] = sprite.Bytes, [earlySprite.Address] = earlySprite.Bytes };
        foreach (NativeBattleGfAnimationSource source in programmes.Values)
        {
            fronts.Add(source.Address, source.Data);
        }
        fronts.Add(MeshAddresses[^1], meshes[MeshAddresses[^1]].Bytes);
        foreach (uint address in SoundAddresses)
        {
            fronts.Add(address, Bytes(address, 4));
        }
        NativeBattleEffect140Resources result = new(mainTim, model, new("effect140-vibration", fronts[VibrationAddress]), SoundAddresses.Select(address => BinaryPrimitives.ReadUInt32LittleEndian(fronts[address].AsSpan())).ToImmutableArray(), programmes, meshes, sprite, files, fronts.ToImmutableDictionary()) { EarlySprite = earlySprite };
        result.Validate();
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect140Resources? nativeEffect140Resources;

    public NativeBattleEffect140Resources ReadNativeEffect140Resources()
    {
        if (nativeEffect140Resources is null)
        {
            byte[] executable = ReadNativeExecutable();
            NativeExecutableMemory memory = new(executable);
            using IFlArchive magic = OpenArchive("magic");
            using IFlArchive battle = OpenArchive("battle");
            ImmutableDictionary<int, NativeBattleEffectFileAsset>.Builder files = ImmutableDictionary.CreateBuilder<int, NativeBattleEffectFileAsset>();
            for (int id = 528; id <= 536; id++)
            {
                uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)id * 4), 4));
                byte[] nameBytes = memory.Read(pointer, 128);
                int end = nameBytes.AsSpan().IndexOf((byte)0);
                string expected = $"MAG139_H.{id - 528:00}";
                if (end <= 0 || Encoding.ASCII.GetString(nameBytes.AsSpan(0, end)) != expected)
                {
                    throw new InvalidDataException("Effect140 selected file directory changed.");
                }
                files.Add(id, new(checked((short)id), expected, Read(battle, expected).ToImmutableArray()));
            }
            nativeEffect140Resources = NativeBattleEffect140ResourcesReader.Read(executable, new("mag139.tim", Read(magic, "mag139.tim").ToImmutableArray()), files.ToImmutable());
        }
        return nativeEffect140Resources;
    }
}
