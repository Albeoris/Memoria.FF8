using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleBoomerangSword304ResourcesReader
{
    public static NativeBattleBoomerangSword304Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag303.tim\0")
        {
            throw new InvalidDataException("Boomerang Sword requires its original initializer texture identity.");
        }
        NativeBattleBoomerangSword304Resources resources = new(new(ProgrammeAddress, memory.Read(ProgrammeAddress, ProgrammeLength).ToImmutableArray()), new(0, MeshAddress, memory.Read(MeshAddress, MeshLength).ToImmutableArray()), new(0, TrailMeshAddress, memory.Read(TrailMeshAddress, TrailMeshLength).ToImmutableArray()), new(0, SpriteAddress, 0, 0, memory.Read(SpriteAddress, SpriteLength).ToImmutableArray()), new("FF8_EN:10312CC", memory.Read(CameraAddress, CameraLength).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBoomerangSword304Resources? nativeBoomerangSword304Resources;
    public NativeBattleBoomerangSword304Resources ReadNativeBoomerangSword304Resources() => nativeBoomerangSword304Resources ??= NativeBattleBoomerangSword304ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
