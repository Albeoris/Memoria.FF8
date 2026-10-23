using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEnemyMicroMissiles284ResourcesReader
{
    public static NativeBattleEnemyMicroMissiles284Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 15)) != "mag283-298.tim\0")
        {
            throw new InvalidDataException("Enemy Micro Missiles requires its original shared initializer texture identity.");
        }
        NativeBattleEnemyMicroMissiles284Resources resources = new(new(MissileAddress, memory.Read(MissileAddress, MissileLength).ToImmutableArray()), new(ImpactAddress, memory.Read(ImpactAddress, ImpactLength).ToImmutableArray()), new(WideImpactAddress, memory.Read(WideImpactAddress, WideImpactLength).ToImmutableArray()), new(0, ParticleSpriteAddress, 0, 0, memory.Read(ParticleSpriteAddress, ParticleSpriteLength).ToImmutableArray()), new(1, TrailSpriteAddress, 0, 0, memory.Read(TrailSpriteAddress, TrailSpriteLength).ToImmutableArray()), new(2, ImpactSpriteAddress, 0, 0, memory.Read(ImpactSpriteAddress, ImpactSpriteLength).ToImmutableArray()), new("FF8_EN:1094DA4", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEnemyMicroMissiles284Resources? nativeEnemyMicroMissiles284Resources;
    public NativeBattleEnemyMicroMissiles284Resources ReadNativeEnemyMicroMissiles284Resources() => nativeEnemyMicroMissiles284Resources ??= NativeBattleEnemyMicroMissiles284ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
