using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleOilBlast137ResourcesReader
{
    public static NativeBattleOilBlast137Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(NativeBattleOilBlast137Profile.TextureNameAddress, 11)) != "mag136.tim\0")
        {
            throw new InvalidDataException("Oil Blast requires its original initializer texture name.");
        }
        NativeBattleGfAnimationSource Act(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
        NativeBattleOilBlast137Resources resources = new(Act(NativeBattleOilBlast137Profile.OpeningActAddress, 0x6C8), Act(NativeBattleOilBlast137Profile.MainActAddress, 0x18E8), Act(NativeBattleOilBlast137Profile.ImpactActAddress, 0x3348), new(0, NativeBattleOilBlast137Profile.SpriteAddress, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, NativeBattleOilBlast137Profile.SpriteAddress)), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, NativeBattleOilBlast137Profile.StaticMeshAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, NativeBattleOilBlast137Profile.CustomMeshAddress), new("FF8_EN:11F0108", memory.Read(NativeBattleOilBlast137Profile.CameraAddress, NativeBattleOilBlast137Profile.CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(NativeBattleOilBlast137Profile.SoundAddress, 4)), new(0, NativeBattleOilBlast137Profile.MatrixAddress, 0, 0, memory.Read(NativeBattleOilBlast137Profile.MatrixAddress, 32).ToImmutableArray()));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleOilBlast137Resources? nativeOilBlast137Resources;
    public NativeBattleOilBlast137Resources ReadNativeOilBlast137Resources() => nativeOilBlast137Resources ??= NativeBattleOilBlast137ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
