using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDuel274ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(2);
        for (short id = 715; id <= 716; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)id * 4), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int length = bytes.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native Duel archive directory requires its original terminated file name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(bytes.AsSpan(0, length)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleDuel274Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0x10E778C, 0x10E7958, 0x10E7A34, 0x10E7ADC, 0x10E7C54, 0x10E7868 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        NativeBattleDuel274Resources result = new(memory.Read(NativeBattleDuel274Resources.Address, checked((int)(NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address))).ToImmutableArray(), sprites) { Files = ReadFileNames(executable), Meshes = new uint[] { 0x10E7E18, 0x10E9360, 0x10E9530 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray() };
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDuel274Resources? nativeDuel274Resources;

    public NativeBattleDuel274Resources ReadNativeDuel274Resources()
    {
        if (nativeDuel274Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            NativeBattleDuel274Resources original = NativeBattleDuel274ResourcesReader.Read(executable);
            nativeDuel274Resources = original with { Files = original.Files.Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray() };
        }
        return nativeDuel274Resources;
    }
}
