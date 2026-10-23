using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect183Profile;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect183ResourcesReader
{
    public static NativeBattleEffect183Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(TextureNameAddress, 11)) != "mag182.tim\0")
        {
            throw new InvalidDataException("Effect183 requires its original initializer texture name.");
        }
        byte[] colors = memory.Read(ColorsAddress, 40);
        NativeBattleEffect183Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshAAddress), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, MeshBAddress), Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(colors.AsSpan(index * 4, 4))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SoundAddress, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect183Resources? nativeEffect183Resources;
    public NativeBattleEffect183Resources ReadNativeEffect183Resources() => nativeEffect183Resources ??= NativeBattleEffect183ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
