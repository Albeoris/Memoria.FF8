using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Logical resource access shared by engine asset consumers.</summary>
public static class GameDataResources
{
    public static Memoria.FF8.Runtime.NativeFieldRuntimeProfile RequireNativeRuntimeProfile(ResourceService resources)
    {
        return resources.GetSource<IGameRuntimeSource>()?.RequireNativeRuntimeProfile() ?? throw new NotSupportedException("The resource source does not provide native runtime contracts.");
    }

    public static byte[] ReadNativeExecutable(string gameDirectory)
    {
        using IResourceSource source = GameDataSourceFactory.Open(gameDirectory);
        return ReadNativeExecutable(new ResourceService(source));
    }

    public static byte[] ReadNativeExecutable(ResourceService resources)
    {
        return resources.Read(new ResourceKey("runtime/native-executable"));
    }
}
