using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCuragaResourcesReader
{
    public static NativeBattleCuragaResources Read(byte[] executable, ushort effect = 28)
    {
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForEffect(effect);
        uint animationAddress = profile.AnimationAddress;
        ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, 8);
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan().Slice(6));
        if (length != 0xBA0 || BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan()) != 2)
        {
            throw new InvalidDataException("Native87DB10 animation does not end at its original asset string.");
        }
        NativeBattleEffectHeapAllocation animation = new($"FF8_EN:{animationAddress:X}", NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, length));
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, profile.SoundAddress, 4).AsSpan());
        ImmutableArray<byte> flash = NativeBattleEffectStaticResourcesReader.Read(executable, profile.FlashAddress, 12);
        return new(animation, sound) { Effect = effect, Executable = ReadExecutableSource(executable, effect), FlashProgram = Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(flash.AsSpan().Slice(4 * index))).ToImmutableArray() };
    }

    public static NativeBattleCuragaExecutableSource ReadExecutableSource(byte[] executable, ushort effect = 28)
    {
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect);
        ImmutableArray<byte> originalBank = NativeBattleEffectStaticResourcesReader.Read(executable, profile.ExecutableAddress, profile.ExecutableLength);
        if (BinaryPrimitives.ReadUInt32LittleEndian(originalBank.AsSpan()) != 0)
        {
            throw new InvalidDataException("Native Curaga catalog requires the original uninitialized executable resource.");
        }
        return new(originalBank) { ResourceAddress = profile.ExecutableAddress };
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCuragaResources? nativeCuragaResources;

    public NativeBattleCuragaResources ReadNativeCuragaResources()
    {
        return nativeCuragaResources ??= NativeBattleCuragaResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
