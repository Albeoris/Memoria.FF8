using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRaldo19ResourcesReader
{
    public static NativeBattleRaldo19Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> overlay = memory.Read(0x162A470, 8).ToImmutableArray();
        NativeBattleRaldo19Resources resources = new(new("FF8_EN:162A204", memory.Read(NativeBattleRaldo19Profile.CameraAddress, NativeBattleRaldo19Profile.CameraLength).ToImmutableArray()), new(memory.Read(NativeBattleRaldo19Profile.ExecutableAddress, NativeBattleRaldo19Profile.ExecutableLength).ToImmutableArray()) { ResourceAddress = NativeBattleRaldo19Profile.ExecutableAddress }, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1629FAC, 4)), new(0x162A470, [BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan()), BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan().Slice(4))]), NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, 0x1629FB0), NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, 0x162A0C0), memory.ReadLoader(NativeBattleRaldo19Profile.SharedAddress, 84).ToImmutableArray(), memory.ReadLoader(NativeBattleRaldo19Profile.BallAddress, 0xC4).ToImmutableArray(), memory.ReadLoader(NativeBattleRaldo19Profile.BodyScratchAddress, NativeBattleRaldo19Profile.BodyScratchLength).ToImmutableArray(), memory.ReadLoader(NativeBattleRaldo19Profile.DescriptorAddress, 48).ToImmutableArray(), memory.ReadLoader(NativeBattleRaldo19Profile.VertexScratchAddress, NativeBattleRaldo19Profile.VertexScratchLength).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.ReadLoader(0x273048C, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.ReadLoader(0x272DDDC, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRaldo19Resources? nativeRaldo19Resources;

    public NativeBattleRaldo19Resources ReadNativeRaldo19Resources()
    {
        return nativeRaldo19Resources ??= NativeBattleRaldo19ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
