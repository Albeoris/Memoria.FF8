using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleCarbuncle278ResourcesReader
{
    public static NativeBattleCarbuncle278Resources Read(byte[] executable) => Read(new NativeExecutableMemory(executable));

    public static NativeBattleCarbuncle278Resources Read(IGameNativeMemorySource memory)
    {
        NativeBattleCarbuncle278Resources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x10C0D08u));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleCarbuncle278Resources? nativeCarbuncle278Resources;

    public NativeBattleCarbuncle278Resources ReadNativeCarbuncle278Resources()
    {
        return nativeCarbuncle278Resources ??= NativeBattleCarbuncle278ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
    }
}
