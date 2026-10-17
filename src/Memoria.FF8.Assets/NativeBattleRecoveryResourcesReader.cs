using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRecoveryResourcesReader
{
    public static NativeBattleRecoveryExecutableState ReadColdProcessState(byte[] executable, ushort effect)
    {
        return ReadColdProcessState(new NativeExecutableMemory(executable), effect);
    }

    public static NativeBattleRecoveryExecutableState ReadColdProcessState(IGameNativeMemorySource memory, ushort effect)
    {
        NativeBattleRecoveryResources resources = Read(memory, effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        byte[] channels = memory.ReadLoader(profile.ChannelAddress, profile.ChannelLength);
        if (channels.Any(value => value != 0))
        {
            throw new InvalidDataException("Native recovery channel bank contradicts its authenticated loader-zero storage.");
        }
        return NativeBattleRecoveryExecutableState.FromAuthenticatedColdProcess(resources.Executable, channels.ToImmutableArray());
    }

    public static NativeBattleRecoveryResources Read(byte[] executable, ushort effect)
    {
        return Read(new NativeExecutableMemory(executable), effect);
    }

    public static NativeBattleRecoveryResources Read(IGameNativeMemorySource memory, ushort effect)
    {
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(memory, profile.ExecutableAddress, profile.ExecutableLength);
        NativeBattleRecoveryExecutableSource source = new(effect, bytes);
        NativeBattleRecoveryExecutableState original = new(effect, bytes);
        original.Validate();
        if (original.InitializationFlag != 1)
        {
            throw new InvalidDataException("Native recovery source directory requires original flag1/absolute pointers.");
        }
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(memory, profile.SoundAddress, 4).AsSpan());
        string filename = System.Text.Encoding.ASCII.GetString(NativeBattleEffectStaticResourcesReader.Read(memory, profile.FilenameAddress, profile.TextureName.Length + 1).AsSpan());
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
                        ImmutableArray<byte> sprite = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, pointer);
                        _ = original.Read(pointer, sprite.Length);
                        break;
                    case 2:
                        NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, pointer);
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
            resources = NativeBattleRecoveryResourcesReader.Read(ReadNativeMemory(), effect);
            nativeRecoveryResources.Add(effect, resources);
        }
        return resources;
    }
}
