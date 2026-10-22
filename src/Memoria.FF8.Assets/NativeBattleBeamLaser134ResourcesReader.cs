using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBeamLaser134ResourcesReader
{
    public static NativeBattleBeamLaser134Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x12077A8, 11)) != "mag133.tim\0")
        {
            throw new InvalidDataException("Beam Laser requires its original initializer texture name.");
        }
        uint[] addresses = [0x12038DC, 0x1204004, 0x1205270, 0x12065F0, 0x1207150];
        int[] lengths = [0x728, 0x126C, 0x1380, 0xB60, 0x52C];
        ImmutableArray<NativeBattleGfAnimationSource> acts = addresses.Select((address, index) => new NativeBattleGfAnimationSource(address, memory.Read(address, lengths[index]).ToImmutableArray())).ToImmutableArray();
        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
        NativeBattleBeamLaser134Resources resources = new(new("FF8_EN:1207680", memory.Read(0x1207680, 0x128).ToImmutableArray()), acts, Sprite(0x1201EBC), Sprite(0x1202068), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1202214u), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x120767C, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBeamLaser134Resources? nativeBeamLaser134Resources;
    public NativeBattleBeamLaser134Resources ReadNativeBeamLaser134Resources() => nativeBeamLaser134Resources ??= NativeBattleBeamLaser134ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
