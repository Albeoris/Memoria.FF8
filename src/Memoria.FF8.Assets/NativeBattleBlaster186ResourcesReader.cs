using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleBlaster186ResourcesReader
{
    public static NativeBattleBlaster186Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag185.tim\0")
        {
            throw new InvalidDataException("Blaster requires its original initializer texture name.");
        }
        ImmutableArray<NativeBattleBlaster186VertexShape> shapes = SourceAddresses.Select(address => new NativeBattleBlaster186VertexShape(address - 8, address, Dword(address - 8), checked((int)Dword(address - 4)), memory.Read(address, VertexFrameBytes).ToImmutableArray())).ToImmutableArray();
        ImmutableArray<uint> pointers = Enumerable.Range(0, 20).Select(index => Dword(VertexPointersAddress + (uint)(index * 4))).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 8).Select(index => Dword(ColorsAddress + (uint)(index * 4))).ToImmutableArray();
        ImmutableArray<int> timings = Enumerable.Range(0, 7).Select(index => unchecked((int)Dword(TimingsAddress + (uint)(index * 4)))).ToImmutableArray();
        ImmutableArray<int> forward = Enumerable.Range(0, 3).Select(index => unchecked((int)Dword(ForwardAddress + (uint)(index * 4)))).ToImmutableArray();
        NativeBattleBlaster186Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshCAddress), Sprite(SpriteAAddress), Sprite(SpriteBAddress), Sprite(SpriteCAddress), Sprite(SpriteDAddress), new("FF8_EN:D1DA60", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), Dword(SoundAddress), shapes, pointers, colors, timings, forward);
        resources.Validate();
        return resources;

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBlaster186Resources? nativeBlaster186Resources;
    public NativeBattleBlaster186Resources ReadNativeBlaster186Resources() => nativeBlaster186Resources ??= NativeBattleBlaster186ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
