using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDrinkMagic37ResourcesReader
{
    public static NativeBattleDrinkMagic37Resources Read(byte[] executable)
    {
        NativeBattleDrinkMagic37Resources resources = new(new("FF8_EN:15D8EB4", NativeBattleEffectStaticResourcesReader.Read(executable, NativeBattleDrinkMagic37Profile.CameraAddress, NativeBattleDrinkMagic37Profile.CameraLength)), new(NativeBattleEffectStaticResourcesReader.Read(executable, NativeBattleDrinkMagic37Profile.ExecutableAddress, NativeBattleDrinkMagic37Profile.ExecutableLength)) { ResourceAddress = NativeBattleDrinkMagic37Profile.ExecutableAddress }, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, 0x15D92F4), NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, 0x15D9504), NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, 0x15D9624), NativeBattleEffectStaticResourcesReader.Read(executable, NativeBattleDrinkMagic37Profile.OrbAddress, NativeBattleDrinkMagic37Profile.OrbLength), NativeBattleEffectStaticResourcesReader.Read(executable, NativeBattleDrinkMagic37Profile.ShapeAddress, NativeBattleDrinkMagic37Profile.ShapeLength), BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x15D62E8, 4).AsSpan()));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDrinkMagic37Resources? nativeDrinkMagic37Resources;

    public NativeBattleDrinkMagic37Resources ReadNativeDrinkMagic37Resources()
    {
        return nativeDrinkMagic37Resources ??= NativeBattleDrinkMagic37ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
