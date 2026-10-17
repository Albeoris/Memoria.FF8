using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect107ResourcesReader
{
    public static NativeBattleEffect107Resources Read(byte[] executable)
    {
        ImmutableArray<byte> bank = NativeBattleEffectStaticResourcesReader.Read(executable, NativeBattleEffect107Resources.Address, NativeBattleEffect107Resources.Length);
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x12EC358, 0x12EC8F8, 0x12EC7CC, 0x12EC574, 0x12EC414, 0x12EC6D4 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(executable, address)).ToImmutableArray();
        NativeBattleEffectHeapAllocation animation = new("FF8_EN:12F3068", NativeBattleEffectStaticResourcesReader.Read(executable, 0x12F3068, 0x90C));
        return new(bank, animation, sprites, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x12F3974, 4).AsSpan()));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect107Resources? nativeEffect107Resources;

    public NativeBattleEffect107Resources ReadNativeEffect107Resources()
    {
        return nativeEffect107Resources ??= NativeBattleEffect107ResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
