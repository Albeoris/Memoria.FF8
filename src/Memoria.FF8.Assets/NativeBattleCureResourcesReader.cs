using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCureResourcesReader
{
    public static NativeBattleCureResources Read(byte[] executable, ushort effect = 1)
    {
        return Read(new NativeExecutableMemory(executable), effect);
    }

    public static NativeBattleCureResources Read(IGameNativeMemorySource memory, ushort effect = 1)
    {
        NativeBattleCureFamilyProfile profile = NativeBattleCureFamilyProfile.ForEffect(effect);
        uint animationAddress = profile.AnimationAddress;
        ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(memory, animationAddress, 8);
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan().Slice(6));
        if (length != profile.Sprite(0x1641F78) - animationAddress || BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan()) != 2)
        {
            throw new InvalidDataException("Native8D6A00 animation directory does not end at its original first sprite descriptor.");
        }
        NativeBattleEffectHeapAllocation animation = new($"FF8_EN:{animationAddress:X}", NativeBattleEffectStaticResourcesReader.Read(memory, animationAddress, length));
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x1641F78, 0x1641FCC, 0x164205C, 0x1642238, 0x1642504 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(memory, profile.Sprite(address))).ToImmutableArray();
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(memory, profile.SoundAddress, 4).AsSpan());
        return new(animation, sprites, sound) { Effect = effect };
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCureResources? nativeCureResources;

    public NativeBattleCureResources ReadNativeCureResources()
    {
        return nativeCureResources ??= NativeBattleCureResourcesReader.Read(ReadNativeMemory());
    }
}
