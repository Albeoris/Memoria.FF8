using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRecoveryResourcesReader
{
    public static NativeBattleRecoveryExecutableState ReadColdProcessState(byte[] executable, ushort effect)
    {
        NativeBattleRecoveryResources resources = Read(executable, effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        using PEReader image = new(new MemoryStream(executable));
        long rva = profile.ChannelAddress - (long)image.PEHeaders.PEHeader!.ImageBase;
        SectionHeader section = image.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + profile.ChannelLength <= (long)section.VirtualAddress + section.VirtualSize);
        if (rva < (long)section.VirtualAddress + section.SizeOfRawData)
        {
            throw new InvalidDataException("Native recovery channel bank is not wholly in its authenticated loader-zero virtual tail.");
        }
        return NativeBattleRecoveryExecutableState.FromAuthenticatedColdProcess(resources.Executable, new byte[profile.ChannelLength].ToImmutableArray());
    }

    public static NativeBattleRecoveryResources Read(byte[] executable, ushort effect)
    {
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(executable, profile.ExecutableAddress, profile.ExecutableLength);
        NativeBattleRecoveryExecutableSource source = new(effect, bytes);
        NativeBattleRecoveryExecutableState original = new(effect, bytes);
        original.Validate();
        if (original.InitializationFlag != 1)
        {
            throw new InvalidDataException("Native recovery source directory requires original flag1/absolute pointers.");
        }
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, profile.SoundAddress, 4).AsSpan());
        string filename = System.Text.Encoding.ASCII.GetString(NativeBattleEffectStaticResourcesReader.Read(executable, profile.FilenameAddress, profile.TextureName.Length + 1).AsSpan());
        if (filename != profile.TextureName + "\0")
        {
            throw new InvalidDataException("Native recovery resource filename contradicts its original initializer.");
        }
        int[] counts = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = U32(profile.DirectoryAddress + 4u + (uint)(table * 64 + index * 4));
                if (pointer == 0)
                {
                    continue;
                }
                counts[table]++;
                switch (table)
                {
                    case 0:
                        byte[] node = original.Read(pointer, 0x158);
                        for (int field = 0xC8; field <= 0x144; field += 4)
                        {
                            uint channel = BinaryPrimitives.ReadUInt32LittleEndian(node.AsSpan(field));
                            if (channel >= profile.ChannelAddress && channel < profile.ChannelAddress + profile.ChannelLength)
                            {
                                continue;
                            }
                            _ = original.Read(channel, 1);
                        }
                        break;
                    case 1:
                        ImmutableArray<byte> sprite = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, pointer);
                        _ = original.Read(pointer, sprite.Length);
                        break;
                    case 2:
                        NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(executable, pointer);
                        _ = original.Read(pointer, mesh.Bytes.Length);
                        break;
                    case 3:
                        // Shape indices/ratios are selected by the current channels; their actual reached reads remain bounded by this bank.
                        _ = original.Read(pointer, 8);
                        break;
                }
            }
        }
        int[] expected = effect == 53 ? [6, 2, 3, 3] : [10, 2, 5, 2];
        if (!counts.SequenceEqual(expected))
        {
            throw new InvalidDataException("Native recovery bank does not have its authenticated four original directories.");
        }
        return new(effect, sound, source);

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(original.Read(address, 4));
    }
}

public sealed partial class GameAssets
{
    private readonly Dictionary<ushort, NativeBattleRecoveryResources> nativeRecoveryResources = [];

    public NativeBattleRecoveryResources ReadNativeRecoveryResources(ushort effect)
    {
        _ = NativeBattleRecoveryProfile.ForEffect(effect);
        if (!nativeRecoveryResources.TryGetValue(effect, out NativeBattleRecoveryResources? resources))
        {
            resources = NativeBattleRecoveryResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), effect);
            nativeRecoveryResources.Add(effect, resources);
        }
        return resources;
    }
}
