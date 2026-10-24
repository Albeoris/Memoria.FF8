using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect79ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return Enumerable.Range(396, 6).Select(index =>
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)index * 4), 4));
            byte[] data = memory.Read(pointer, 128);
            int length = data.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Effect79 requires its original terminated file directory names.");
            }
            return new NativeBattleEffectFileAsset(checked((short)index), Encoding.ASCII.GetString(data.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleEffect79Resources Read(byte[] executable, NativeBattleEffectInitializerAsset mainTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag078.tim\0")
        {
            throw new InvalidDataException("Effect79 requires its selected original initializer texture name.");
        }
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != names.Length || files.Where((file, index) => file.FileId != names[index].FileId || file.Name != names[index].Name).Any())
        {
            throw new InvalidDataException("Effect79 input files disagree with the original native file directory.");
        }
        ImmutableDictionary<uint, NativeBattleEffect79NodeResourceBank> banks = NodeSourceLengths.ToImmutableDictionary(pair => pair.Key, pair => new NativeBattleEffect79NodeResourceBank(pair.Key, Bytes(pair.Key, pair.Value)));
        foreach (NativeBattleEffect79NodeResourceBank bank in banks.Values)
        {
            bank.Validate();
            for (int index = 0; index < 16; index++)
            {
                uint sprite = bank.Directory(1, index);
                if (sprite != 0)
                {
                    RequireBounded(sprite, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, bank.Address + sprite).Length, bank.Data.Length);
                }
                uint mesh = bank.Directory(2, index);
                if (mesh != 0)
                {
                    RequireBounded(mesh, NativeBattleEffectStaticResourcesReader.ReadMesh(memory, bank.Address + mesh).Bytes.Length, bank.Data.Length);
                }
                uint morph = bank.Directory(3, index);
                if (morph != 0)
                {
                    int vertices = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(bank.Address + morph + 4, 4));
                    int length = checked(8 + vertices * 8);
                    if (vertices is not (95 or 125) || BinaryPrimitives.ReadInt32LittleEndian(memory.Read(bank.Address + morph, 4)) != length)
                    {
                        throw new InvalidDataException("Effect79 requires its original bounded morph-source rows.");
                    }
                    RequireBounded(morph, length, bank.Data.Length);
                }
            }
        }
        NativeBattleEffect79Resources result = new(mainTim, new("FF8_EN:1581BCC", Bytes(CameraAddress, CameraLength)), new("FF8_EN:176E064", Bytes(VibrationAddress, VibrationLength)), banks, new("FF8_EN:1581F18", Bytes(ScriptAddress, ScriptLength)), files.ToImmutableDictionary(file => file.FileId), new uint[] { 0x1580B24, 0x1580B28, 0x1580B2C }.ToImmutableDictionary(address => address, address => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4))), new uint[] { 0x1580B30, 0x1580D78, 0x1580E54 }.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address)), Enumerable.Range(0, 18).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(PointSelectorsAddress + (uint)index * 2, 2))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(TargetPointerAddress, 4)));
        result.Validate();
        int[] workspaces = [236, 308, 308, 536];
        int[] nodes = [3, 3, 3, 6];
        int[] frames = [15, 40, 40, 40];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleGfAnimationSource source = result.ReadProgramme(ProgrammeAddresses[index]);
            int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
            if (BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan()) != 1 || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table)) != nodes[index] || BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)) != frames[index])
            {
                throw new InvalidDataException("Effect79 loaded programme requires its original geometry/node/frame directory.");
            }
            NativeBattleGfAnimationWorkspace workspace = new(new byte[workspaces[index]].ToImmutableArray());
            workspace = workspace.Initialize7016B0(source, workspaces[index]);
            for (int frame = 0; frame < frames[index]; frame++)
            {
                NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
                {
                    if (node.GeometryIndex != 0)
                    {
                        throw new InvalidDataException("Effect79 loaded programme selects an absent geometry front.");
                    }
                    return current;
                });
                if (next.Remaining != frames[index] - frame - 1)
                {
                    throw new InvalidDataException("Effect79 loaded programme changed its complete original duration.");
                }
                workspace = next.Workspace;
            }
        }
        return result;

        ImmutableArray<byte> Bytes(uint address, int length) => memory.Read(address, length).ToImmutableArray();
    }

    private static void RequireBounded(uint offset, int length, int capacity)
    {
        if (length < 0 || (ulong)offset + (uint)length > (uint)capacity)
        {
            throw new InvalidDataException("Effect79 node payload crosses its exact original source-bank boundary.");
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect79Resources? nativeEffect79Resources;

    public NativeBattleEffect79Resources ReadNativeEffect79Resources()
    {
        if (nativeEffect79Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleEffect79ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectInitializerAsset tim = new("mag078.tim", Read(magic, "mag078.tim").ToImmutableArray());
            nativeEffect79Resources = NativeBattleEffect79ResourcesReader.Read(executable, tim, files);
        }
        return nativeEffect79Resources;
    }
}
