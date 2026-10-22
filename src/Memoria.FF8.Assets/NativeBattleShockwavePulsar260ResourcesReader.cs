using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShockwavePulsar260ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, [360, 361]);

    public static NativeBattleShockwavePulsar260Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x112D1A4, 15)) != "mag069-259.tim\0")
        {
            throw new InvalidDataException("Shockwave Pulsar requires its original initializer texture name.");
        }
        uint[] addresses = [0x111D424, 0x111DEBC, 0x111F09C, 0x112015C, 0x112A974];
        int[] lengths = [2712, 4576, 4288, 43032, 10268];
        NativeBattleShockwavePulsar260Resources result = new(new("FF8_EN:111B3CC", memory.Read(0x111B3CC, 0x1B74).ToImmutableArray()), addresses.Select((address, index) => new NativeBattleGfAnimationSource(address, memory.Read(address, lengths[index]).ToImmutableArray())).ToImmutableArray(), new uint[] { 0x111CF4C, 0x111D0F8 }.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray(), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x111D30C), memory.Read(0x112D190, 16).ToImmutableArray(), memory.Read(0x112D1B4, 15).ToImmutableArray(), Enumerable.Range(0, 3).Select(index => new NativeBattleShockwavePulsar260Sound(0x111CF40u + (uint)index * 4, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x111CF40u + (uint)index * 4, 4)))).ToImmutableArray(), files);
        ValidatePayloads(result);
        return result;
    }

    public static void ValidatePayloads(NativeBattleShockwavePulsar260Resources resources)
    {
        resources.Validate();
        int[] geometries = [1, 2, 2, 15, 3];
        int[] nodes = [2, 3, 3, 55, 5];
        int[] durations = [22, 200, 30, 182, 200];
        int[] workspaces = [0xD8, 0x100, 0xBC, 0x1100, 0x218];
        for (int index = 0; index < resources.Acts.Length; index++)
        {
            ValidateAct(resources.Acts[index], geometries[index], nodes[index], durations[index], workspaces[index]);
        }
        int[] expectedCustom = [6, 9, 10, 11];
        if (!Enumerable.Range(0, resources.MainDispatch.Length).Where(index => resources.MainDispatch[index] == 0x31).SequenceEqual(expectedCustom))
        {
            throw new InvalidDataException("Shockwave Pulsar changes its original specialized geometry selection.");
        }
        for (int index = 0; index < resources.PuffSprites.Length; index++)
        {
            NativeBattleModuleResource sprite = resources.PuffSprites[index];
            int count = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8));
            if (count != (index == 0 ? 16 : 20))
            {
                throw new InvalidDataException("Shockwave Pulsar puff frame directory is incomplete.");
            }
            for (int phase = 0; phase < count; phase++)
            {
                _ = sprite.ProjectedFrame571C80(phase);
            }
        }
        foreach (NativeBattleEffectFileAsset file in resources.Files)
        {
            if (WorldAssetReader.ReadTim(file.Data.ToArray(), 0).End != file.Data.Length)
            {
                throw new InvalidDataException("Shockwave Pulsar streamed texture has an incomplete native TIM boundary.");
            }
        }
        ReadOnlySpan<byte> dat = resources.CasterDat.Data.AsSpan();
        int[] sections = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(dat, index) != sections[index])
            {
                throw new InvalidDataException("Shockwave Pulsar caster binding directory is incomplete.");
            }
        }
        if (NativeBattlePoseDecoder.ReadFrameCounts(dat.Slice(sections[0], sections[1] - sections[0])).Length != 9)
        {
            throw new InvalidDataException("Shockwave Pulsar requires its original caster animation directory.");
        }
    }

    private static void ValidateAct(NativeBattleGfAnimationSource source, int geometryCount, int nodeCount, int duration, int workspaceBytes)
    {
        int animation = Int(source.Data, 4);
        if (Int(source.Data, 0) != geometryCount || Int(source.Data, animation) != nodeCount || Int(source.Data, animation + 4) != duration)
        {
            throw new InvalidDataException("Shockwave Pulsar ACT directory changes its original programme shape.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] vertices = new int[geometryCount];
        int[] variants = new int[geometryCount];
        for (int index = 0; index < geometryCount; index++)
        {
            int start = Int(source.Data, 8 + index * 4);
            int end = index + 1 < geometryCount ? Int(source.Data, 12 + index * 4) : animation;
            int faceOffset = Int(source.Data, start);
            vertices[index] = Int(source.Data, start + 4);
            variants[index] = Int(source.Data, start + 8);
            if (vertices[index] <= 0 || variants[index] <= 0 || vertices[index] > (end - start - 12) / 8 / variants[index] || faceOffset != 12 + vertices[index] * variants[index] * 8 || variants[index] > 1 && vertices[index] * 8 > NativeBattleShockwavePulsar260Profile.MorphBytes)
            {
                throw new InvalidDataException("Shockwave Pulsar ACT vertices leave their immutable geometry or shared morph span.");
            }
            int cursor = checked(start + faceOffset);
            for (int group = 0; group < 8; group++)
            {
                int count = Int(source.Data, cursor);
                cursor = checked(cursor + 4);
                if (count < 0 || count > (end - cursor) / strides[group])
                {
                    throw new InvalidDataException("Shockwave Pulsar ACT primitive group leaves its original geometry.");
                }
                for (int face = 0; face < count; face++)
                {
                    int corners = (group & 1) == 0 ? 3 : 4;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(cursor + face * strides[group] + 4 + corner * 2, 2));
                        if ((vertex & 1) != 0 || vertex / 2 >= vertices[index])
                        {
                            throw new InvalidDataException("Shockwave Pulsar ACT polygon selects an absent packed vertex.");
                        }
                    }
                }
                cursor = checked(cursor + count * strides[group]);
            }
            if (cursor != end)
            {
                throw new InvalidDataException("Shockwave Pulsar ACT primitive groups do not close their original geometry.");
            }
        }
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[workspaceBytes].ToImmutableArray()).Initialize7016B0(source, workspaceBytes);
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (node.GeometryIndex >= geometryCount || node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= variants[node.GeometryIndex] || node.Word42 >= variants[node.GeometryIndex])
                {
                    throw new InvalidDataException("Shockwave Pulsar ACT animation selects an absent geometry variant.");
                }
                return current;
            });
            if (step.Remaining != duration - frame - 1)
            {
                throw new InvalidDataException("Shockwave Pulsar ACT animation changes its original duration.");
            }
            workspace = step.Workspace;
        }
    }

    private static int Int(ImmutableArray<byte> data, int offset)
    {
        if (offset < 0 || offset > data.Length - 4)
        {
            throw new InvalidDataException("Shockwave Pulsar resource read leaves its supplied immutable span.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleShockwavePulsar260Resources? nativeShockwavePulsar260Resources;
    public NativeBattleShockwavePulsar260Resources ReadNativeShockwavePulsar260Resources()
    {
        if (nativeShockwavePulsar260Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleShockwavePulsar260ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeShockwavePulsar260Resources = NativeBattleShockwavePulsar260ResourcesReader.Read(executable, files);
        }
        return nativeShockwavePulsar260Resources;
    }
}
