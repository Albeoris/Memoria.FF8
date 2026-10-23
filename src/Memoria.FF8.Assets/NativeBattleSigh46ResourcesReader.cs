using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSigh46Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleSigh46ResourcesReader
{
    public static NativeBattleSigh46Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag045.tim\0")
        {
            throw new InvalidDataException("Sigh requires its original initializer texture name.");
        }
        foreach (NativeBattleSigh46Range channel in ChannelRegions)
        {
            if (memory.ReadLoader(channel.Address, channel.Length).Any(value => value != 0))
            {
                throw new InvalidDataException("Sigh original loader channels disagree with their cold-zero source.");
            }
        }
        NativeBattleSigh46Resources resources = new(new(memory.Read(SourceAddress, SourceLength).ToImmutableArray()) { ResourceAddress = SourceAddress }, new(0, StaticAddress, 0, 0, memory.Read(StaticAddress, StaticLength).ToImmutableArray()), [new(0, 0x15b82a4, 0, 0, memory.Read(0x15b82a4, 404).ToImmutableArray()), new(1, 0x15b8438, 0, 0, memory.Read(0x15b8438, 404).ToImmutableArray()), new(2, 0x15b85cc, 0, 0, memory.Read(0x15b85cc, 612).ToImmutableArray()), new(3, 0x15b8830, 0, 0, memory.Read(0x15b8830, 612).ToImmutableArray()), new(4, 0x15b8a94, 0, 0, memory.Read(0x15b8a94, 272).ToImmutableArray()), new(5, 0x15b8ba4, 0, 0, memory.Read(0x15b8ba4, 428).ToImmutableArray())], [NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress)], [new(0, 0x15bcd10, 0, 0, memory.Read(0x15bcd10, 68).ToImmutableArray()), new(1, 0x15c0d14, 0, 0, memory.Read(0x15c0d14, 68).ToImmutableArray())], [new(0, 0x15b8d50, 0, 0, memory.Read(0x15b8d50, 960).ToImmutableArray()), new(1, 0x15b9110, 0, 0, memory.Read(0x15b9110, 960).ToImmutableArray()), new(2, 0x15b94d0, 0, 0, memory.Read(0x15b94d0, 960).ToImmutableArray()), new(3, 0x15b9890, 0, 0, memory.Read(0x15b9890, 960).ToImmutableArray()), new(4, 0x15b9c50, 0, 0, memory.Read(0x15b9c50, 960).ToImmutableArray()), new(5, 0x15ba010, 0, 0, memory.Read(0x15ba010, 960).ToImmutableArray()), new(6, 0x15ba3d0, 0, 0, memory.Read(0x15ba3d0, 960).ToImmutableArray()), new(7, 0x15ba790, 0, 0, memory.Read(0x15ba790, 960).ToImmutableArray()), new(8, 0x15bab50, 0, 0, memory.Read(0x15bab50, 960).ToImmutableArray()), new(9, 0x15baf10, 0, 0, memory.Read(0x15baf10, 960).ToImmutableArray()), new(10, 0x15bb2d0, 0, 0, memory.Read(0x15bb2d0, 960).ToImmutableArray()), new(11, 0x15bb690, 0, 0, memory.Read(0x15bb690, 960).ToImmutableArray()), new(12, 0x15bba50, 0, 0, memory.Read(0x15bba50, 960).ToImmutableArray()), new(13, 0x15bbe10, 0, 0, memory.Read(0x15bbe10, 960).ToImmutableArray()), new(14, 0x15bc1d0, 0, 0, memory.Read(0x15bc1d0, 960).ToImmutableArray()), new(15, 0x15bc590, 0, 0, memory.Read(0x15bc590, 960).ToImmutableArray()), new(16, 0x15bc950, 0, 0, memory.Read(0x15bc950, 960).ToImmutableArray()), new(17, 0x15bcd54, 0, 0, memory.Read(0x15bcd54, 960).ToImmutableArray()), new(18, 0x15bd114, 0, 0, memory.Read(0x15bd114, 960).ToImmutableArray()), new(19, 0x15bd4d4, 0, 0, memory.Read(0x15bd4d4, 960).ToImmutableArray()), new(20, 0x15bd894, 0, 0, memory.Read(0x15bd894, 960).ToImmutableArray()), new(21, 0x15bdc54, 0, 0, memory.Read(0x15bdc54, 960).ToImmutableArray()), new(22, 0x15be014, 0, 0, memory.Read(0x15be014, 960).ToImmutableArray()), new(23, 0x15be3d4, 0, 0, memory.Read(0x15be3d4, 960).ToImmutableArray()), new(24, 0x15be794, 0, 0, memory.Read(0x15be794, 960).ToImmutableArray()), new(25, 0x15beb54, 0, 0, memory.Read(0x15beb54, 960).ToImmutableArray()), new(26, 0x15bef14, 0, 0, memory.Read(0x15bef14, 960).ToImmutableArray()), new(27, 0x15bf2d4, 0, 0, memory.Read(0x15bf2d4, 960).ToImmutableArray()), new(28, 0x15bf694, 0, 0, memory.Read(0x15bf694, 960).ToImmutableArray()), new(29, 0x15bfa54, 0, 0, memory.Read(0x15bfa54, 960).ToImmutableArray()), new(30, 0x15bfe14, 0, 0, memory.Read(0x15bfe14, 960).ToImmutableArray()), new(31, 0x15c01d4, 0, 0, memory.Read(0x15c01d4, 960).ToImmutableArray()), new(32, 0x15c0594, 0, 0, memory.Read(0x15c0594, 960).ToImmutableArray()), new(33, 0x15c0954, 0, 0, memory.Read(0x15c0954, 960).ToImmutableArray())], Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(DelayAddress + (uint)index * 2, 2))).ToImmutableArray(), Enumerable.Range(0, 16).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(FadeAddress + (uint)index * 2, 2))).ToImmutableArray(), new("FF8_EN:15C349C", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleSigh46Resources? nativeSigh46Resources;
    public NativeBattleSigh46Resources ReadNativeSigh46Resources() => nativeSigh46Resources ??= NativeBattleSigh46ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
