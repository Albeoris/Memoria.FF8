using Memoria.FF8.Resources;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Assets;

/// <summary>Logical resource access shared by engine asset consumers.</summary>
public static class GameDataResources
{
    public static IGameNativeMemorySource ReadNativeMemory(ResourceService resources)
    {
        return resources.GetSource<IGameNativeMemorySource>() ?? throw new NotSupportedException("The resource source has no authenticated native memory contract.");
    }

    private static readonly ConditionalWeakTable<ResourceService, NativeExecutableImage> ExecutableImages = new();

    private sealed class NativeExecutableImage
    {
        public readonly byte[] Bytes;

        public NativeExecutableImage(ResourceService resources)
        {
            Bytes = resources.Read(new ResourceKey("runtime/native-executable"));
        }
    }

    public static Memoria.FF8.Runtime.NativeFieldRuntimeProfile RequireNativeRuntimeProfile(ResourceService resources)
    {
        return resources.GetSource<IGameRuntimeSource>()?.RequireNativeRuntimeProfile() ?? throw new NotSupportedException("The resource source does not provide native runtime contracts.");
    }

    public static byte[] ReadNativeExecutable(string gameDirectory)
    {
        using IResourceSource source = GameDataSourceFactory.Open(gameDirectory);
        return ReadNativeExecutable(new ResourceService(source));
    }

    /// <summary>Returns the source-scoped, authenticated image. Consumers must treat its shared bytes as read-only.</summary>
    public static byte[] ReadNativeExecutable(ResourceService resources)
    {
        return ExecutableImages.GetValue(resources, static source => new NativeExecutableImage(source)).Bytes;
    }
}
