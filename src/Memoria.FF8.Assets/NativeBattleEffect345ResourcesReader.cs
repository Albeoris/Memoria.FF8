using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect345Resources;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect345ResourcesReader
{
    public static NativeBattleEffect345Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, TextureName.Length + 1)) != TextureName + "\0")
        {
            throw new InvalidDataException("Effect345 requires its original initializer texture name.");
        }
        ImmutableArray<byte> bytes = memory.Read(ModelAddress, ModelLength).ToImmutableArray();
        int[] sections = Enumerable.Range(0, 5).Select(index => NativeBattleDatDirectory.Section(bytes.AsSpan(), index)).ToArray();
        if (!sections.SequenceEqual(new[] { 24, 1384, 11816, 16332, 16332 }))
        {
            throw new InvalidDataException("Effect345 requires its complete original four-section model.");
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<byte> frameCounts = NativeBattlePoseDecoder.ReadFrameCounts(pose.Animations.AsSpan());
        foreach (int animation in new[] { 5, 6 })
        {
            if (animation >= frameCounts.Length)
            {
                throw new InvalidDataException("Effect345 selected animation leaves its original model.");
            }
            for (int frame = 1; frame <= frameCounts[animation]; frame++)
            {
                _ = pose.Sample(checked((byte)animation), checked((byte)frame), 0);
            }
        }
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        NativeBattleEffectOwnedModelResource model = new(ModelAddress, bytes, pose, geometry);
        ImmutableArray<NativeBattleEffect345NodeResourceBank> nodes = new[] { (Address: 0x1643698u, Length: 18932), (Address: 0x164808Cu, Length: 9708) }.Select(source => new NativeBattleEffect345NodeResourceBank(source.Address, memory.Read(source.Address, source.Length).ToImmutableArray())).ToImmutableArray();
        foreach (NativeBattleEffect345NodeResourceBank bank in nodes)
        {
            bank.Validate();
            for (int index = 0; index < 16; index++)
            {
                uint sprite = bank.Directory(1, index);
                if (sprite != 0)
                {
                    RequireBounded(bank, sprite, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, bank.Address + sprite).Length);
                }
                uint mesh = bank.Directory(2, index);
                if (mesh != 0)
                {
                    RequireBounded(bank, mesh, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, bank.Address + mesh).Bytes.Length);
                }
                uint morph = bank.Directory(3, index);
                if (morph != 0)
                {
                    int count = BinaryPrimitives.ReadInt32LittleEndian(bank.Data.AsSpan().Slice(checked((int)morph + 4), 4));
                    int length = checked(8 + count * 8);
                    if (count != 84 || BinaryPrimitives.ReadInt32LittleEndian(bank.Data.AsSpan().Slice(checked((int)morph), 4)) != length)
                    {
                        throw new InvalidDataException("Effect345 requires both original eighty-four-vertex morph sources.");
                    }
                    RequireBounded(bank, morph, length);
                }
            }
        }
        ImmutableDictionary<uint, ImmutableArray<byte>> staticSpans = new (uint Address, int Length)[] { (0x152465C, 4), (0x1524660, 4), (0x1529720, 30), (0x1529740, 30), (0x1529760, 30), (0x1529780, 30) }.ToImmutableDictionary(span => span.Address, span => memory.Read(span.Address, span.Length).ToImmutableArray());
        NativeBattleEffect345Resources result = new(mainTim, new("FF8_EN:164A678", memory.Read(EmbeddedTim0Address, EmbeddedTim0Length).ToImmutableArray()), new("FF8_EN:1652A98", memory.Read(EmbeddedTim1Address, EmbeddedTim1Length).ToImmutableArray()), new("FF8_EN:1524774", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), model, nodes, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAddress), staticSpans);
        result = result with { ColdPointerSeeds = new uint[] { 0x1524658, 0x15297A0 }.ToImmutableDictionary(address => address, address => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4))) };
        result.Validate();
        return result;
    }

    private static void RequireBounded(NativeBattleEffect345NodeResourceBank bank, uint offset, int length)
    {
        if (length < 0 || (ulong)offset + (uint)length > (ulong)bank.Data.Length)
        {
            throw new InvalidDataException("Effect345 selected node payload leaves its exact original source.");
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect345Resources? nativeEffect345Resources;

    public NativeBattleEffect345Resources ReadNativeEffect345Resources()
    {
        if (nativeEffect345Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectInitializerAsset mainTim = new(TextureName, Read(magic, TextureName).ToImmutableArray());
            nativeEffect345Resources = NativeBattleEffect345ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources), mainTim);
        }
        return nativeEffect345Resources;
    }
}
