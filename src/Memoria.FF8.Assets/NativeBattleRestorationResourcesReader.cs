using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRestorationResourcesReader
{
    public static NativeBattleRestorationResources Read(byte[] executable, ushort effect)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        uint animationAddress = profile.AnimationAddress;
        uint nameAddress = profile.AssetNameAddress;
        byte[] header = NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, 8).ToArray();
        int length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6));
        if (BinaryPrimitives.ReadUInt16LittleEndian(header) != 2 || animationAddress + length != nameAddress)
        {
            throw new InvalidDataException("Native restoration animation does not end at its own original asset string.");
        }
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress - 4, 4).AsSpan());
        NativeBattleEffectHeapAllocation animation = new($"FF8_EN:{animationAddress:X}", NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, length));
        System.Collections.Immutable.ImmutableArray<byte> directory = NativeBattleEffectStaticResourcesReader.Read(executable, profile.ExecutableAddress, 0x104);
        if (BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan()) != 0)
        {
            throw new InvalidDataException("Native restoration catalog requires its original uninitialized executable directory.");
        }
        NativeBattleRestorationExecutableSource source = new(effect, NativeBattleEffectStaticResourcesReader.Read(executable, profile.ExecutableAddress, NativeBattleRestorationExecutableState.Length(effect)));
        return new(effect, animation, sound) { OriginalExecutableDirectory = directory, Executable = source };
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRestorationResources? nativeEsunaResources;
    private NativeBattleRestorationResources? nativeFullLifeResources;
    private NativeBattleRestorationResources? nativeEffect57Resources;
    private NativeBattleRestorationResources? nativeRemedyResources;

    public NativeBattleRestorationResources ReadNativeRestorationResources(ushort effect)
    {
        return effect switch
        {
            24 => nativeEsunaResources ??= NativeBattleRestorationResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), effect),
            27 => nativeFullLifeResources ??= NativeBattleRestorationResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), effect),
            57 => nativeEffect57Resources ??= NativeBattleRestorationResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), effect),
            67 => nativeRemedyResources ??= NativeBattleRestorationResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")), effect),
            _ => throw new NotSupportedException("The shared restoration resource reader supports only native24/27/57/67.")
        };
    }
}
