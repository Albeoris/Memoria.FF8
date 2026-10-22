using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEnergyBomber217ResourcesReader
{
    public static NativeBattleEnergyBomber217Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(NativeBattleEnergyBomber217Profile.TextureNameAddress, 11)) != "mag216.tim\0")
        {
            throw new InvalidDataException("Energy Bomber requires its original initializer texture name.");
        }
        NativeBattleEnergyBomber217Resources resources = new(new(NativeBattleEnergyBomber217Profile.MainActAddress, memory.Read(NativeBattleEnergyBomber217Profile.MainActAddress, 0x4900).ToImmutableArray()), new(NativeBattleEnergyBomber217Profile.ImpactActAddress, memory.Read(NativeBattleEnergyBomber217Profile.ImpactActAddress, 0x1BC0).ToImmutableArray()), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, NativeBattleEnergyBomber217Profile.TrailMeshAddress), new(0, NativeBattleEnergyBomber217Profile.SpriteAddress, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, NativeBattleEnergyBomber217Profile.SpriteAddress)), new("FF8_EN:11355D4", memory.Read(NativeBattleEnergyBomber217Profile.CameraAddress, NativeBattleEnergyBomber217Profile.CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(NativeBattleEnergyBomber217Profile.SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEnergyBomber217Resources? nativeEnergyBomber217Resources;
    public NativeBattleEnergyBomber217Resources ReadNativeEnergyBomber217Resources() => nativeEnergyBomber217Resources ??= NativeBattleEnergyBomber217ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
