using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleMagmaBreath48ResourcesReader
{
    public static NativeBattleMagmaBreath48Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag047.tim\0")
        {
            throw new InvalidDataException("Magma Breath requires its original initializer texture name.");
        }
        foreach (NativeBattleMagmaBreath48Range channel in ChannelRegions)
        {
            if (memory.ReadLoader(channel.Address, channel.Length).Any(value => value != 0))
            {
                throw new InvalidDataException("Magma Breath original loader channels disagree with their cold-zero source.");
            }
        }
        NativeBattleMagmaBreath48Resources resources = new(new(memory.Read(SourceAddress, SourceLength).ToImmutableArray()) { ResourceAddress = SourceAddress }, new(0, StaticAddress, 0, 0, memory.Read(StaticAddress, StaticLength).ToImmutableArray()), [new(0, 0x15adc6c, 0, 0, memory.Read(0x15adc6c, 428).ToImmutableArray()), new(0, 0x15ade18, 0, 0, memory.Read(0x15ade18, 428).ToImmutableArray()), new(0, 0x15adfc4, 0, 0, memory.Read(0x15adfc4, 428).ToImmutableArray()), new(0, 0x15ae170, 0, 0, memory.Read(0x15ae170, 428).ToImmutableArray()), new(0, 0x15ae31c, 0, 0, memory.Read(0x15ae31c, 480).ToImmutableArray())], [NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress)], [new(0, 0x15b252c, 0, 0, memory.Read(0x15b252c, 1464).ToImmutableArray()), new(1, 0x15b309c, 0, 0, memory.Read(0x15b309c, 1464).ToImmutableArray()), new(2, 0x15b3654, 0, 0, memory.Read(0x15b3654, 1464).ToImmutableArray()), new(3, 0x15b3c0c, 0, 0, memory.Read(0x15b3c0c, 1464).ToImmutableArray()), new(4, 0x15b41c4, 0, 0, memory.Read(0x15b41c4, 1464).ToImmutableArray()), new(5, 0x15b477c, 0, 0, memory.Read(0x15b477c, 1464).ToImmutableArray()), new(6, 0x15b4d34, 0, 0, memory.Read(0x15b4d34, 1464).ToImmutableArray()), new(7, 0x15b52ec, 0, 0, memory.Read(0x15b52ec, 1464).ToImmutableArray()), new(8, 0x15b58a4, 0, 0, memory.Read(0x15b58a4, 1464).ToImmutableArray()), new(9, 0x15b5e5c, 0, 0, memory.Read(0x15b5e5c, 1464).ToImmutableArray())], new("FF8_EN:15B7D8C", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)), Enumerable.Range(0, 16).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(ColorScriptAddress + (uint)index * 4, 4))).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMagmaBreath48Resources? nativeMagmaBreath48Resources;
    public NativeBattleMagmaBreath48Resources ReadNativeMagmaBreath48Resources() => nativeMagmaBreath48Resources ??= NativeBattleMagmaBreath48ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
